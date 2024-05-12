namespace NodoAme.Models;

/// <summary>
/// ピッチ解析結果で値が0未満の場合の処理
/// </summary>
public enum NoPitchModes{
	//何もしない
	NONE,

	//デフォルトピッチ削除
	REMOVE,
}