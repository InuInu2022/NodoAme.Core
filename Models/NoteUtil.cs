using System;

namespace NodoAme.Models
{
    public static class NoteUtil
    {
        public static int FreqToMidiNoteNumber(double freq)
		{
			int midinote = (int)Math.Round(69.0 + (12.0 * Math.Log(freq / 440.0, 2)));
			return midinote;
		}

        public static (int octave, int step) FreqToPitchOctaveAndStep(double freq){
			int num = FreqToMidiNoteNumber(freq);
			var oc = num == 0 ? -1 : (num / 12) - 1;
			var st = num == 0 ? 0 : num % 12;
			return (oc, st);
		}
    }

    public enum NoteAdaptMode{
        /// <summary>
		/// 固定
		/// </summary>
        FIXED,
        /// <summary>
		/// 平均
		/// </summary>
        AVERAGE,
        /// <summary>
		/// 中央値
		/// </summary>
        MEDIAN
    }

    public enum NoteSplitModes{
        /// <summary>
		/// sil, pauなどを無視し、分割する
		/// </summary>
        IGNORE_NOSOUND,
        /// <summary>
		/// sil, pauは分割に使うが無視しない
		/// </summary>
        SPLIT_ONLY
    }
}
