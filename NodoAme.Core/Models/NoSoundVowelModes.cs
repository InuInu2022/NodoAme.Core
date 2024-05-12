namespace NodoAme.Models;

/// <summary>
/// 無声母音をどう表現するか
/// </summary>
public enum NoSoundVowelsModes{
	/// <summary>
	/// VOLで表現
	/// </summary>
	VOLUME,

	/// <summary>
	/// あいまい母音の音素（AIUEO）で表現
	/// </summary>
	PHONEME,

	/// <summary>
	/// VOLとあいまい母音の音素（AIUEO）で表現
	/// </summary>
	VOL_PHONEME,
}