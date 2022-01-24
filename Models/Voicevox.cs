using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using RestSharp;
using RestSharp.Extensions;
using System.IO;
using NAudio.Wave;
using System.Threading;
using NLog;

namespace NodoAme.Models
{
    public class Voicevox
    {
		public bool IsActive { get; set; } = false;
        public string[]? AvailableCasts { get; set; }

        public double SpeedScale { get; set; }

        public double PitchScale { get; set; }

        public double IntonationScale { get; set; }

        public double VolumeScale { get; set; }

        public double PrePhonemeLength { get; set; }

        public double PostPhonemeLength { get; set; }

		public List<VoicevoxCast> VoicevoxCasts { get; set; } = new List<VoicevoxCast>();

        public string Cast {
            get{
				return voice?.Name ?? AvailableCasts![0];
			}
            set{
				var (cast, index) = VoicevoxCasts
                    .Select((c,i)=>(cast:c,index:i))
                    .ToList()
					.Find(item => item.cast.Name == value);
				this.voice = new TalkSoftVoice
				{
                    Id = cast.Styles![0].Id.ToString(),
                    Name = cast.Name
				};
			}
        }

        public TalkSoftVoiceStylePreset Style{
			get
			{
				return this.style ?? new TalkSoftVoiceStylePreset();
			}
			set
			{
				this.style = new TalkSoftVoiceStylePreset
				{
					Id = value.Id,
					Name = value.Name
				};
			}
		}

		private readonly TalkSoft talkSoft;
		private TalkSoftVoice? voice;
		private TalkSoftVoiceStylePreset? style;
		private readonly string? host;
		private readonly RestClient? restClient;
		private readonly string engineType;

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		internal Voicevox(
            string engineType,
            TalkSoft talkSoft,
			TalkSoftVoice? voice = null,
			TalkSoftVoiceStylePreset? style = null
        ){
            this.engineType = engineType;
			this.talkSoft = talkSoft;
			this.voice = voice;
			this.style = style;

            host = talkSoft.Interface!.RestHost;
            if(string.IsNullOrEmpty(host)) {
				logger.Error($"host:{host} not found");
                MessageBox.Show(
                    $"{engineType}が見つかりませんでした。\n{host}は無効な文字列です。",
                    $"{engineType}の呼び出しに失敗",
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error
                );
				return;
			};
            restClient = new RestClient();
            if(restClient is null){
                logger.Error("restClient is null!");
				return;
			};
		}

        /// <summary>
		/// VoiceVoxインスタンスを作成するFactoryメソッド
		/// </summary>
		/// <param name="engineType"></param>
		/// <param name="talkSoft"></param>
		/// <param name="voice"></param>
		/// <param name="style"></param>
		/// <returns>VoiceVoxインスタンス</returns>
        public static async ValueTask<Voicevox> Factory(
            string engineType,
            TalkSoft talkSoft,
			TalkSoftVoice? voice = null,
			TalkSoftVoiceStylePreset? style = null
        ){
			var init = new Voicevox(engineType, talkSoft, voice, style);
            if(init.restClient is null) return init;
            await init.CheckActiveAsync();
			await init.GetAvailableCastsAsync();
			return init;
		}

        public async ValueTask<Label[]> GetPhonemes(string serif){
			var (labels, _) = await GetPhonemesAndLength(serif);
			return labels;
		}

        public async ValueTask<(Label[] labels, double length)> GetPhonemesAndLength(string serif){
            if(string.IsNullOrEmpty(serif)){
				return (new Label[] { new Label("", 0, 0) }, 0.0);
			}
			var param = new (string,string)[]{
					("text",serif),
					("speaker", style?.Id ?? 0.ToString())
				};
			var res = await InternalPostRequest(
                "audio_query",
				queries: param
			);
            if(res.IsSuccessful && !(res is null)){
				var content = res.Content ?? "{}";
				var root = await CastContent<AudioQueryResponse>(content);
				SetEngineParams(root);
				var moras = root
					.AccentPhrases
					.Select(a => {
						if (a.Moras is null) { return a.Moras; }
						if (a.PauseMora is null) { return a.Moras; }

                        var m = a.Moras;
                        var p = a.PauseMora;
                        m.Add(p);
                        return m;
					})
                    .SelectMany(m => m)
                    .Where(m => !(m is null))
					;
				var labels = new List<Label>();
				double total = root.PrePhonemeLength;

				labels.Add(new Label("sil",0, total));  //前余白

				foreach (var mora in moras)
                {
                    if(!string.IsNullOrEmpty(mora.Consonant)){
						labels.Add(new Label (
                            mora.Consonant!,
                            total,
                            total + (mora.ConsonantLength ?? 0)
                        ));
						total += mora.ConsonantLength ?? 0;
					}
                    if(!string.IsNullOrEmpty(mora.Vowel) && 
                    !(mora.VowelLength is null)){
						labels.Add(new Label(
                            mora.Vowel!,
                            total,
                            total + (mora.VowelLength ?? 0)
                        ));
						total += mora.VowelLength ?? 0;
					}
                }
				labels.Add(new Label("sil",
					total,
                    total+root.PostPhonemeLength
                ));  //後余白
				total += root.PostPhonemeLength;
				return (labels.ToArray(), total);

			}else if(!(res is null))
			{
				CheckResponce(res);
				throw new Exception("response error!");
			}else{
				const string? msg = "ERROR: response is null!";
				Debug.WriteLine(msg);
				logger.Error(msg);
				throw new NullReferenceException(msg);
			}
        }

        public async ValueTask SpeakAsync(string serif){
            if(string.IsNullOrEmpty(serif))return;


			var sRes = await SynthesisAsync(serif);

			await Task.Run(() =>
			{
				using var ms = new MemoryStream(sRes.RawBytes);
				var rs = new RawSourceWaveStream(
					ms,
					new WaveFormat(24000, 16, 1)
				);
				var wo = new WaveOutEvent();
				wo.Init(rs);
				wo.Play();
				while (wo.PlaybackState == PlaybackState.Playing)
				{
					Thread.Sleep(500);
				}
				wo.Dispose();
			});

			//var dl = await restClient!.DownloadDataAsync(sRes);
		}

        public async ValueTask<bool> OutputWaveToFile(
            string serif,
            string path
        ){
            if(string.IsNullOrEmpty(serif))return false;
            if(string.IsNullOrEmpty(path))return false;

            var sRes = await SynthesisAsync(serif);

            await Task.Run(
                ()=>File.WriteAllBytes(path, sRes.RawBytes));
			//using WaveFileWriter writer = new WaveFileWriter(path, new WaveFormat(24000, 16, 1));
			//writer.Write(sRes.RawBytes, 0, sRes.RawBytes?.Length ?? 0);
			//await writer.WriteAsync(sRes.RawBytes, 0, sRes.RawBytes?.Length ?? 0);
			return true;
		}

        private async ValueTask<RestResponse> SynthesisAsync(string serif){
            var query = new (string,string)[]{
					("text",serif),
					("speaker", style?.Id ?? 0.ToString())
				};
            var res = await InternalPostRequest(
                "audio_query",
				queries: query
			);

			if (!res.IsSuccessful)
			{
				CheckResponce(res);
				throw new Exception("response error!");
			}

            var root = await CastContent<AudioQueryResponse>(res?.Content ?? "{}");
            var synthQuery = new (string, string)[]{
                ("speaker", voice?.Id ?? 0.ToString())
			};
			var sRes = await InternalPostRequest(
				"synthesis",
				synthQuery,
				root
			);

            if(!sRes.IsSuccessful){
				CheckResponce(sRes);
				throw new Exception("response error!");
			}
            if(sRes is null){
				logger.Error("voicevox synthesis responce is null");
				throw new Exception("response error!");
            }

			return sRes;
		}



		private async ValueTask CheckActiveAsync()
		{
			RestResponse res = await InternalGetRequest("version");
			if (res.IsSuccessful)
			{
				const string Message = "Voicevox REST connect success!";
				Debug.WriteLine(Message);
				logger.Info(Message);
				IsActive = true;
			}
			else
			{
				MessageBox.Show(
					$"{engineType}が起動していない、または見つかりません。\n{res.ErrorMessage}",
					$"{engineType} Initialize Failed",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				logger.Error($"checkversion:{engineType}が起動していない、または見つかりません。\n{res.ErrorMessage}");
				IsActive = false;
			}
		}

        /// <summary>
		/// 内部Getリクエスト
		/// </summary>
		/// <param name="requestString"></param>
		/// <param name="queries"></param>
		/// <returns>response</returns>
		private async Task<RestResponse> InternalGetRequest(
            string requestString,
            (string queryName, string queryData)[]? queries = null
        )
		{
			CheckRestClient();
			var req = new RestRequest($"{host}{requestString}", Method.Get);
			AddQueryParameter(queries, req);
			var res = await restClient!.ExecuteGetAsync(req);
			CheckResponce(res);
			return res;
		}


        /// <summary>
		/// 内部POSTリクエスト
		/// </summary>
		/// <param name="requestString"></param>
		/// <param name="queries"></param>
		/// <param name="jsonBody">匿名型でOK</param>
		/// <returns>response</returns>
		private async Task<RestResponse> InternalPostRequest(
            string requestString,
            (string queryName, string queryData)[]? queries = null,
            AudioQueryResponse? jsonBody = null
        ){
            CheckRestClient();
            var req = new RestRequest($"{host}{requestString}", Method.Post);
            AddQueryParameter(queries, req);
            if(!(jsonBody is null))
			{
				SetEngineParams(jsonBody);

				req.AddJsonBody(jsonBody);
			}
			var res = await restClient!.ExecutePostAsync(req);
			CheckResponce(res);
			return res;
        }

		private void SetEngineParams(AudioQueryResponse? jsonBody)
		{
			//set engine params option
			jsonBody.IntonationScale = this.IntonationScale;
			jsonBody.PitchScale = this.PitchScale;
			jsonBody.PostPhonemeLength = this.PostPhonemeLength;
			jsonBody.PrePhonemeLength = this.PrePhonemeLength;
			jsonBody.SpeedScale = this.SpeedScale;
			jsonBody.VolumeScale = this.VolumeScale;
		}

		private static void AddQueryParameter((string queryName, string queryData)[]? queries, RestRequest req)
		{
			if (!(queries is null) && queries.Length > 0)
			{
				foreach (var (queryName, queryData) in queries)
				{
					req.AddParameter(queryName, queryData, ParameterType.QueryString);
				}
			}
		}

		private void CheckResponce(RestResponse res)
		{
			if (!res.IsSuccessful)
			{
				var msg = $"エラーコード:{res.StatusCode}\nエラー内容：{res.ErrorException}\n{res.ErrorMessage}";
				MessageBox.Show(
					msg,
					$"{engineType} への通信失敗",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				Debug.WriteLine($"REST responce:\n{res}");
				logger.Error(msg);
			}
		}

		private void CheckRestClient()
		{
			if (restClient is null)
			{
				MessageBox.Show(
					$"{engineType}が起動していない、または見つかりません。",
					$"{engineType} への通信失敗",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				logger.Error($"CheckRestClient: restClient is null");
				throw new Exception($"{engineType}が起動していない、または見つかりません。");
			}
		}

        private void ShowError(RestResponse res)
		{
			MessageBox.Show(
				$"{engineType}が起動していない、または見つかりません。\n{res.ErrorMessage}",
				$"{engineType} Initialize Failed",
				MessageBoxButton.OK,
				MessageBoxImage.Error
			);
			Debug.WriteLine($"Response ERROR:\n{res}");
		}

		private async Task<T> CastContent<T>(string content){
			using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            try{
                T settings = await JsonSerializer
                .DeserializeAsync<T>
                (
                    stream,
                    new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder
                            .Create(UnicodeRanges.All),
                        WriteIndented = true,
                    }
                );
                if (settings is null)
                {
					const string Message = "JSON読み取りに失敗！:setting is null";
                    logger.Error(Message);
					throw new Exception(Message);
                }
                return settings;
            }catch(Exception e){
				Debug.WriteLine($"ERROR:{e}");
				logger.Error($"CastContent:{e.Message}");
				throw new Exception("JSON読み取りに失敗！");
			}
		}

		private async ValueTask<string[]> GetAvailableCastsAsync(){
			var req = await InternalGetRequest("speakers");
			//req.Content
			var content = req?.Content ?? "{}";
			var casts = await CastContent<VoicevoxCast[]>(content);

			var castNames = new List<string>();
			foreach (var cast in casts)
            {
				//castNames.
                if(cast is null || cast.Name is null)continue;
				castNames.Add(cast.Name);
				VoicevoxCasts.Add(cast);
			}
			this.AvailableCasts = castNames.ToArray();
			return this.AvailableCasts;
		}
    }

    public partial class VoicevoxCast
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("speaker_uuid")]
        public string? SpeakerUuid { get; set; }

        [JsonPropertyName("styles")]
        public VoicevoxStyle[]? Styles { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    public partial class VoicevoxStyle
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("id")]
        public long? Id { get; set; }
    }

    public class Mora
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("consonant")]
        public string? Consonant { get; set; }

        [JsonPropertyName("consonant_length")]
        public double? ConsonantLength { get; set; }

        [JsonPropertyName("vowel")]
        public string? Vowel { get; set; }

        [JsonPropertyName("vowel_length")]
        public double? VowelLength { get; set; }

        [JsonPropertyName("pitch")]
        public double Pitch { get; set; }
    }

    public class AccentPhras
    {
        [JsonPropertyName("moras")]
        public List<Mora>? Moras { get; set; }

        [JsonPropertyName("accent")]
        public int Accent { get; set; }

        [JsonPropertyName("pause_mora")]
        public Mora? PauseMora { get; set; }
    }

    public class AudioQueryResponse
    {
        [JsonPropertyName("accent_phrases")]
        public List<AccentPhras>? AccentPhrases { get; set; }

        [JsonPropertyName("speedScale")]
        public double SpeedScale { get; set; }

        [JsonPropertyName("pitchScale")]
        public double PitchScale { get; set; }

        [JsonPropertyName("intonationScale")]
        public double IntonationScale { get; set; }

        [JsonPropertyName("volumeScale")]
        public double VolumeScale { get; set; }

        [JsonPropertyName("prePhonemeLength")]
        public double PrePhonemeLength { get; set; }

        [JsonPropertyName("postPhonemeLength")]
        public double PostPhonemeLength { get; set; }

        [JsonPropertyName("outputSamplingRate")]
        public double OutputSamplingRate { get; set; }

        [JsonPropertyName("outputStereo")]
        public bool OutputStereo { get; set; }

        [JsonPropertyName("kana")]
        public string? Kana { get; set; }
    }


}
