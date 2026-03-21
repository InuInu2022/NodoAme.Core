using System.Text.RegularExpressions;

namespace NodoAme;

public static class PhonemeConverter{
	const int PREV_PHONEME_IDY = 1;
	const int CURRENT_PHONEME_IDY = 2;
	const int NEXT_PHONEME_IDY = 3;

	static readonly char[] SEP = ['^', '-', '+', '='];
	static readonly char[] separator = [','];

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
		ITalkWrapper talkEngine,
		string sourceText,
		bool isUseSeparatorSpace = true,
		bool isCheckJapaneseSyllabicNasal = false,
		bool isCheckJapaneseNasalSonantGa = false,
		Models.VowelOptions vowelOption = Models.VowelOptions.DoNothing,
		bool isDebugOutput = false,
		bool isConvertToHiragana = false
	)
	{
		if (Regex.IsMatch(sourceText, @"^\s+"))
		{
			return "";
		}

		var labels = await talkEngine.GetLabelsAsync(sourceText);
		return ConvertCore(
			isUseSeparatorSpace,
			isCheckJapaneseSyllabicNasal,
			isCheckJapaneseNasalSonantGa,
			vowelOption,
			isConvertToHiragana,
			labels);
	}

	public static async ValueTask<string> ConvertAsync(
		IList<string> labels,
		bool isUseSeparatorSpace = true,
		bool isCheckJapaneseSyllabicNasal = false,
		bool isCheckJapaneseNasalSonantGa = false,
		Models.VowelOptions vowelOption = Models.VowelOptions.DoNothing,
		bool isConvertToHiragana = false
	){
		return await Task.Run(()
			=> ConvertCore(
				isUseSeparatorSpace,
				isCheckJapaneseSyllabicNasal,
				isCheckJapaneseNasalSonantGa,
				vowelOption,
				isConvertToHiragana,
				labels));
	}

	private static string ConvertCore(
		bool isUseSeparatorSpace,
		bool isCheckJapaneseSyllabicNasal,
		bool isCheckJapaneseNasalSonantGa,
		VowelOptions vowelOption,
		bool isConvertToHiragana,
		IList<string> labels)
	{
		var pList = new List<string>();
		foreach (var label in labels)
		{
			//phoneme only
			var phonemes = GetPhonemeFromContextLabel(label);
			if (phonemes.Length - 1 < CURRENT_PHONEME_IDY)
			{
				continue;
			}

			var p3 = phonemes[CURRENT_PHONEME_IDY];

			switch (p3)
			{
				case "sil":
				case "xx":
					break;

				case "pau":
					{
						pList.Add(" ");
						break;
					}

				case "N":
					{
						if (isCheckJapaneseSyllabicNasal)
						{
							pList.Add(PhonemeConverter.CheckJapaneseSyllabicNasal(phonemes));
						}
						else
						{
							pList.Add(p3);
						}
						break;
					}

				case "g":
					{
						if (isCheckJapaneseNasalSonantGa)
						{
							pList.Add(PhonemeConverter.CheckJapaneseNasalSonantGa(phonemes));
						}
						else
						{
							pList.Add(p3);
						}

						break;
					}

				case "U":
				case "I":
					{
						if (vowelOption == Models.VowelOptions.Small)
						{
							pList.Add(p3.ToLower());
						}
						else if (vowelOption == Models.VowelOptions.Remove)
						{
							//pList.Add("");
						}
						else
						{
							pList.Add(p3);
						}

						break;
					}

				default:
					{
						pList.Add(p3);
						break;
					}
			}
		}

		CurrentPhonemes = pList;

		return isConvertToHiragana switch
		{
			true => ChangeSeparator(
				ConvertToKana(string.Concat(CurrentPhonemes)),
				isUseSeparatorSpace
			),
			false => ChangeSeparator(isUseSeparatorSpace)
		};
	}

	/// <summary>
	/// かなに変換
	/// </summary>
	/// <param name="phonemes">音素文字列</param>
	/// <returns>かな文字列</returns>
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

	public static string ChangeSeparator(bool isUseSeparatorSpace){
		//join with space
		return isUseSeparatorSpace
			? string.Join(" ", CurrentPhonemes)
			: string.Concat(CurrentPhonemes);
	}

	public static string ChangeSeparator(
		string baseText,
		bool isUseSeparatorSpace
	){
		if (isUseSeparatorSpace)
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
	/// 日本語の撥音Nを後続の音素で変化させる
	/// - pau ->
	/// - pauN,uN,oN -> N
	/// - p,b,m -> m
	/// - t,d,n,r -> n ※厳密には「に」は異なるが表現できない
	/// - k,g(,ng) -> ng
	/// </summary>
	/// <param name="phenoms"></param>
	/// <returns></returns>
	private static string CheckJapaneseSyllabicNasal(string[] phenoms)
	{
		return phenoms[NEXT_PHONEME_IDY] switch
		{
			"p" or "py" or "b" or "by" or "m" or "my" => "m",
			"t" or "ty" or "ch" or "ts" or "d" or "jy" or "n" or "ny" or "r" or "ry" => "n",
			"k" or "ky" or "g" or "gy" => "n,g",
			_ => phenoms[CURRENT_PHONEME_IDY],
		};
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
			s = phenoms[PREV_PHONEME_IDY] switch
			{
				//空白もしくは無音の場合、語頭とみなす
				"pau" or "sil" => "g",
				//それ以外は語中
				//語中は鼻濁音
				_ => "n,g",
			};
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
		return [label];
	}

	public static List<Label> SplitLabel(
		this Label label,
		string splitted
	){
		var a = splitted.Split(separator);
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
