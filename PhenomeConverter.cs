using System;
using System.Diagnostics;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodoAme
{
    public static class PhenomeConverter
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
		    bool isDebugOutput = false
        ){
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
                            pList.Add(PhenomeConverter.CheckJapaneseSyllabicNasal(phenoms));
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
                            pList.Add(PhenomeConverter.CheckJapaneseNasalSonantGa(phenoms));
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

			return ChangeSeparater(isUseSeparaterSpace);
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
    }
}
