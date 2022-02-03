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

		/// <summary>
		/// 音素テキストが母音かどうか
		/// </summary>
		/// <param name="pText"></param>
		/// <returns></returns>
		public static bool IsVowel(string? pText){
            if(string.IsNullOrEmpty(pText)) return false;
			return Regex.IsMatch(pText, "[aiueoAIUEO]");
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


        public static bool IsConsonant(string? cText){
            if(string.IsNullOrEmpty(cText)) return false;

            if(Regex.IsMatch(cText, $"{INVALID_PH}|{CL}|{PAU}|{SIL}")){
                //no
				return false;
			}else if(!IsVowel(cText)){
				//yes
				return true;
			}else{
                return false;
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
			return Regex.IsMatch(nText, "[nmN]|ng");
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
