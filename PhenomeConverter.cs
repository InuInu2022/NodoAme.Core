using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using WanaKanaNet;
using System.Linq;
using NodoAme.Models;

namespace NodoAme
{
	public static class PhonemeConverter
    {
		private const int PREV_PHENOME_IDY = 1;
		private const int CURRENT_PHENOME_IDY = 2;
		private const int NEXT_PHENOME_IDY = 3;

		private static readonly char[] SEP = { '^', '-', '+', '=' };

		public static List<string>? CurrentPhonemes { get; private set; }

		/// <summary>
		/// 文字列から音素抽出
		/// </summary>
		/// <param name="talkEngine">TTSエンジン</param>
		/// <param name="sourceText">文字列</param>
		/// <param name="isCheckJapaneseSyllabicNasal">「ん」を変換する</param>
		/// <param name="isCheckJapaneseNasalSonantGa">が行鼻濁音を変換する</param>
		/// <param name="isDebugOutput">Debug出力</param>
		/// <returns>音素</returns>
		public static async ValueTask<string> ConvertAsync(
			Wrapper talkEngine,
			string sourceText,
			bool isUseSeparaterSpace = true,
		    bool isCheckJapaneseSyllabicNasal = false,
			bool isCheckJapaneseNasalSonantGa = false,
			Models.VowelOptions vowelOption = Models.VowelOptions.DoNothing,
		    bool isDebugOutput = false,
			bool isConvertToHiragana = false
		)
		{
			if (Regex.IsMatch(sourceText, @"^\s+")) {
				return ""; 
			};
			
			var pList = new List<string>();
			
			var debugLabels = "";
			var labels = await talkEngine.GetLabelsAsync(sourceText);
			foreach (var label in labels)
			{
				//phonome only
				var phenoms = GetPhonemeFromContextLabel(label);
				var p3 = phenoms[CURRENT_PHENOME_IDY];
				//System.Debug.WriteLine($"{p3},{p}");
                //Debug.WriteLine($"{p3},{p}");
				switch (p3)
				{
					case "sil":
					case "xx":
						break;
					case "pau":
						//phenome += "   ";
						pList.Add(" ");
						break;
					case "N":
						if (isCheckJapaneseSyllabicNasal)
						{
							//phenome += PhenomeConverter.CheckJapaneseSyllabicNasal(phenoms);
                            pList.Add(PhonemeConverter.CheckJapaneseSyllabicNasal(phenoms));
						}
						else
						{
							//phenome += $"{p3}";
                            pList.Add(p3);
						}
						//phenome += " ";
						break;
                    case "g":
                        /*
						phenome +=
							(isCheckJapaneseNasalSonantGa) ?
								PhenomeConverter.CheckJapaneseNasalSonantGa(phenoms) :
								$"{p3}";
                        phenome += " ";
                        */
                        if(isCheckJapaneseNasalSonantGa){
                            pList.Add(PhonemeConverter.CheckJapaneseNasalSonantGa(phenoms));
                        }else{
                            pList.Add(p3);
                        }
						break;
					case "U":
					case "I":
						if(vowelOption == Models.VowelOptions.Small){
							pList.Add(p3.ToLower());
						}else if(vowelOption == Models.VowelOptions.Remove){
							//pList.Add("");
						}else{
							pList.Add(p3);
						}
						break;
					default:
                        pList.Add(p3);
						//phenome += $"{p3}";
						//phenome += " ";
						break;
				}

				

				//full label
				if (isDebugOutput) debugLabels += $"{label}\r\n";
			}

			CurrentPhonemes = pList;

			return isConvertToHiragana switch
			{
				true => ChangeSeparater(
							ConvertToKana(string.Concat(CurrentPhonemes)),
							isUseSeparaterSpace
						),
				false => ChangeSeparater(isUseSeparaterSpace)
			};
		}

		public static string ConvertToKana(string phonemes)
		{
			var kanaOption = new WanaKanaOptions
			{
				CustomKanaMapping = new Dictionary<string, string>()
					{
						{"cl","っ"}
					}
			};
			return WanaKana.ToHiragana(phonemes, kanaOption);
		}

		private static string[] GetPhonemeFromContextLabel(string label){
			const char SPLITTER = '/';
			//var a = new char[] { SPLITTER };
			var p = label.Split(SPLITTER);
			return p[0].Split(SEP);
		}

		public static string ChangeSeparater(bool isUseSeparaterSpace){
			//join with space
			if (isUseSeparaterSpace)
			{
				return String.Join(" ", CurrentPhonemes);
			}
			else
			{
				return String.Concat(CurrentPhonemes);
			}
		}
		public static string ChangeSeparater(
			string baseText,
			bool isUseSeparaterSpace
		){
			if (isUseSeparaterSpace)
			{
				//return String.Join(" ", CurrentPhonemes);
				var s = baseText.ToCharArray().Select(c => new string(c,1)).ToArray();
				return string.Join(" ", s);
			}
			else
			{
				return baseText;
			}
		}

		/// <summary>
		/// かなに変換
		/// </summary>
		/// <param name="talkEngine"></param>
		/// <param name="sourceText"></param>
		/// <param name="isUseSeparaterSpace"></param>
		/// <returns></returns>
		public static async ValueTask<string> ConvertToKana(
			Wrapper talkEngine,
			string sourceText,
			bool isUseSeparaterSpace = true
		){
			//TODO:impliementation
			var moraList = new List<string>();
			var labels = await talkEngine.GetLabelsAsync(sourceText);
			foreach (var label in labels)
			{
				var line = GetPhonemeFromContextLabel(label);
			}

			return ChangeSeparater(isUseSeparaterSpace);
		}

        /// <summary>
		/// 日本語の撥音Nを後続の音素で変化させる
        ///     * pau ->
        ///         * pauN,uN,oN -> N
        ///     * p,b,m -> m
        ///     * t,d,n,r -> n ※厳密には「に」は異なるが表現できない
        ///     * k,g(,ng) -> ng
		/// </summary>
		/// <param name="phenoms"></param>
		/// <returns></returns>
		private static string CheckJapaneseSyllabicNasal(string[] phenoms)
        {
			switch (phenoms[NEXT_PHENOME_IDY])
			{
				/*
                case "pau":
                case "sil":
                    switch (phenoms[PREV_PHENOME_IDY])
                    {
                        case "a":
                        case "i":
                        case "e":
                            s = "n,g";
                            break;
                        case "u":
                        case "o":
                        case "pau":
                        case "sil":
                            s = "N";
                            break;
                        default:
                            s = "N";
                            break;
                    }
                    break;
                */
				case "p":
				case "py":
				case "b":
				case "by":
				case "m":
				case "my":
					return "m";
				case "t":
				case "ty":
				case "ch":
				case "ts":
				case "d":
				case "jy":
				case "n":
				case "ny":
				case "r":
				case "ry":
					return "n";
				case "k":
				case "ky":
				case "g":
				case "gy":
					return "n,g";
				default:
					return phenoms[CURRENT_PHENOME_IDY];
			}
		}

		/// <summary>
		/// ガ行の鼻濁音変換
		/// </summary>
		/// <param name="phenoms"></param>
		/// <param name="isSimple">シンプルモードで変換するか。有効の場合、語頭はg、語中はng</param>
		/// <returns></returns>
		private static string CheckJapaneseNasalSonantGa(
			string[] phenoms,
			bool isSimple = true
		){
			var s = "";
			if (isSimple)
			{
				//シンプルルール：語頭はg, 語中はng
				switch (phenoms[PREV_PHENOME_IDY])
				{
					case "pau":
					case "sil":
						//空白もしくは無音の場合、語頭とみなす
						s = "g";
						break;
					default:
						//それ以外は語中
						//語中は鼻濁音
						s = "n,g";
						break;
				}
			}
			return s;
		}

		/// <summary>
		/// 英語の音素を日本語の音素に変換する
		/// CeVIO Pro ・ CeVIO AI 8.2以降と互換
		/// </summary>
		/// aa:a; ae:e; ah:a; ax:a; ao:o; aw:a,u; axr:a; ay:a,i; b:b; ch:ch; d:d; dd:r; dh:z; eh:e; ey:e,i; f:f; g:g; hh:h; ih:i; iy:i; jh:j; jjh:j; k:k; l:r; ll:r; m:m; mm:N; n:n; nn:N; ng:n; ow:o,u; oy:o,i; p:p; r:r; s:s; sh:sh; t:t; tt:r; th:s; uh:u; uw:u; v:v; w:w; y:y; z:z; zh:j; zz:z; cl:cl;
		/// <param name="phs"></param>
		/// <returns></returns>
		public static List<Label> EnglishToJapanese(List<Label> phs){
			return phs
				.Select(v =>
				{
					var newPhoneme = v.Phoneme switch
					{
						"aa" => "a",
						"ah" => "a",
						"ax" => "a",
						"axr" => "a",
						"ay" => "a,i",
						"aw" => "a,u",
						"cl" => "cl",
						"ae" => "e",
						"eh" => "e",
						"ey" => "e,i",
						"hh" => "h",
						"ih" => "i",
						"iy" => "i",
						"jh" => "j",
						"jjh" => "j",
						"zh" => "j",
						"mm" => "N",
						"nn" => "N",
						"ng" => "n",
						"ao" => "o",
						"oy" => "o,i",
						"ow" => "o,u",
						"dd" => "r",
						"l" => "r",
						"ll" => "r",
						"tt" => "r",
						"th" => "s",
						"uh" => "u",
						"uw" => "u",
						"dh" => "z",
						"zz" => "z",
						_ => v.Phoneme,
					};

					List<Label> list = newPhoneme!.Contains(",") switch
					{
						true => v.SplitLabel(newPhoneme),
						false => v.ReplacePhoneme(newPhoneme)
					};
					return list;
				})
				.SelectMany(v => v)
				.ToList();
		}

		public static List<Label> ReplacePhoneme(
			this Label label,
			string newPhoneme
		){
			label.Phoneme = newPhoneme;
			return new List<Label> { label };
		}

		public static List<Label> SplitLabel(
			this Label label,
			string splited
		){
			var a = splited.Split(new char[]{','});
			var num = a.Length;

			var start = label.StartTime ?? 0;
			var end = label.EndTime ?? 0;

			var length = end - start;
			var span = length / num;

			return a
				.Select((v, i) => new Label(
					v,
					start + (span * i),
					start + (span * (i+1))
				))
				.ToList();

			//return new List<Label> { label };
		}
    }
}
