using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NodoAme
{
	/// <summary>
	/// settings for NodoAme
	/// </summary>
	public class Settings
    {
		// App. version
		[JsonPropertyName("version")]
		public string? Version {get;set;}

        // 使用するトークソフトの選択肢一覧
        [JsonPropertyName("talkSofts")]
        public IList<TalkSoft>? TalkSofts { get; set; }

		// 出力ソングキャスト名選択肢一覧
		[JsonPropertyName("exportSongCasts")]
		public IList<SongCast>? ExportSongCasts { get;set; }
	}
}