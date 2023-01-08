namespace NodoAme.Models;

public enum VowelOptions
{
	/// <summary>
	/// 何もしない
	/// </summary>
	DoNothing,

	/// <summary>
	/// 小文字音素表記に変換
	/// </summary>
	Small,

	/// <summary>
	/// 無声母音 U,I を削除
	/// </summary>
	Remove,
}
