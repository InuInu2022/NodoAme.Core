#nullable enable

namespace NodoAme;

public enum ExportLyricsMode
{
	KANA = 0,       //hiragana lyrics for CS Japanese voice
	PHONEME = 1,    //phoneme lyrics (same as v0.1.0) for AI
	ALPHABET = 2,	//alphabet lyrics for CS English voice
	EN_TO_JA = 3,	//English to japanase convertsion for CeVIO AI v8.2 after, CeVIO Pro
}
