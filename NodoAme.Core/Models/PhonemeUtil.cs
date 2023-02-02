using System;
using System.Text.RegularExpressions;

namespace NodoAme.Models
{
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
		public static bool IsVowel(string? pText){
			if(string.IsNullOrEmpty(pText)) return false;
			return VOWELS_JA.IsMatch(pText);
		}

		/// <summary>
		/// ラベルの音素が母音かどうか
		/// </summary>
		/// <param name="label"></param>
		/// <returns></returns>
		public static bool IsVowel(Label label){
			if(label is null)return false;
			return PhonemeUtil.IsVowel(label.Phoneme);
		}

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

		public static bool IsConsonant(Label label){
			if(label is null)return false;
			return IsConsonant(label.Phoneme);
		}

		/// <summary>
		/// 鼻音かどうか
		/// </summary>
		/// <param name="nText"></param>
		/// <returns></returns>
		public static bool IsNasal(string? nText){
			if(string.IsNullOrEmpty(nText)) return false;
			return NASAL_JA.IsMatch(nText);
		}

		/// <summary>
		/// ラベルの音素が鼻音かどうか
		/// </summary>
		/// <param name="label"></param>
		/// <returns></returns>
		public static bool IsNasal(Label label){
			if(label is null)return false;
			return IsNasal(label.Phoneme);
		}

		/// <summary>
		/// 促音かどうか
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public static bool IsCL(string? text){
			if(string.IsNullOrEmpty(text))return false;
			return text == CL;
		}

		/// <summary>
		/// ラベルの音素が促音かどうか
		/// </summary>
		/// <param name="label"></param>
		/// <returns></returns>
		public static bool IsCL(Label label){
			if(label is null)return false;
			return IsCL(label.Phoneme);
		}

		public static bool IsPau(Label label){
			if(label is null)return false;
			return label.Phoneme == PAU;
		}
	}
}
