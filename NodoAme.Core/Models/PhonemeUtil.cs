using System.Text.RegularExpressions;

namespace NodoAme.Models;

public static class PhonemeUtil
{
	public const string CL = "cl";
	public const string PAU = "pau";
	public const string SIL = "sil";
	public const string INVALID_PH = "xx";

	public static Regex VOWELS_JA = new("[aiueoAIUEO]", RegexOptions.Compiled);
	public static Regex NOSOUND_VOWELS = new("[AIUEO]", RegexOptions.Compiled);
	public static Regex NO_CONSONANT = new($"{INVALID_PH}|{CL}|{PAU}|{SIL}", RegexOptions.Compiled);
	public static Regex NASAL_JA = new("[nmN]|ng", RegexOptions.Compiled);

	/// <summary>
	/// 音素テキストが母音かどうか
	/// </summary>
	/// <param name="pText"></param>
	/// <returns></returns>
	public static bool IsVowel(string? pText) =>
		!string.IsNullOrEmpty(pText) && VOWELS_JA.IsMatch(pText);

	/// <summary>
	/// ラベルの音素が母音かどうか
	/// </summary>
	/// <param name="label"></param>
	/// <returns></returns>
	public static bool IsVowel(Label label) =>
		label is not null && IsVowel(label.Phoneme);

	/// <summary>
	/// 音素テキストが無声母音か？
	/// </summary>
	/// <param name="text"></param>
	/// <returns></returns>
	public static bool IsNoSoundVowel(string text)
		=> NOSOUND_VOWELS.IsMatch(text);

	/// <summary>
	/// 音素が子音かどうか
	/// </summary>
	/// <param name="cText"></param>
	/// <returns></returns>
	public static bool IsConsonant(string? cText){
		if(string.IsNullOrEmpty(cText)) return false;

		if(NO_CONSONANT.IsMatch(cText)){
			//no:子音
			return false;
		}else if(IsVowel(cText)){
			//no:母音
			return false;
		}else{
			//yes
			return true;
		}
	}

	/// <inheritdoc cref="IsConsonant(string?)"/>
    /// <param name="label"></param>
    /// <returns></returns>
	public static bool IsConsonant(Label label) =>
		label is not null && IsConsonant(label.Phoneme);

	/// <summary>
	/// 鼻音かどうか
	/// </summary>
	/// <param name="nText"></param>
	/// <returns></returns>
	public static bool IsNasal(string? nText) =>
		!string.IsNullOrEmpty(nText) && NASAL_JA.IsMatch(nText);

	/// <summary>
	/// ラベルの音素が鼻音かどうか
	/// </summary>
	/// <param name="label"></param>
	public static bool IsNasal(Label label) =>
		label is not null && IsNasal(label.Phoneme);

	/// <summary>
	/// 促音かどうか
	/// </summary>
	/// <param name="text"></param>
	public static bool IsCL(string? text) =>
		!string.IsNullOrEmpty(text) && text == CL;

	/// <summary>
	/// ラベルの音素が促音かどうか
	/// </summary>
	/// <param name="label"></param>
	/// <returns></returns>
	public static bool IsCL(Label label) =>
		label is not null && IsCL(label.Phoneme);

	/// <summary>
	/// ラベルの音素が[sil]かどうか
	/// </summary>
	/// <param name="label"></param>
	/// <seealso cref="IsSil(Label)"/>
	/// <returns></returns>
	public static bool IsPau(Label label) =>
		label?.Phoneme == PAU;

	/// <summary>
	/// ラベルの音素が[pau]かどうか
	/// </summary>
	/// <param name="label"></param>
	/// <seealso cref="IsPau(Label)"/>
	/// <returns></returns>
	public static bool IsSil(Label label) =>
		label?.Phoneme == SIL;
}
