using System;

namespace NodoAme.Models
{
    /// <summary>
	/// ラベルデータ管理クラス
	/// </summary>
    public class Label
    {
        /// <summary>
		/// 音素
		/// </summary>
        public string? Phoneme { get; set; } = "xx";
		public double? StartTime { get; set; }
		public double? EndTime { get; set; }

        public Label(
            string phoneme,
            double startTime,
            double endTime
        ){
			Phoneme = phoneme;
			StartTime = startTime;
			EndTime = endTime;
		}
    }
}
