using System;
using System.Text.Json.Serialization;

namespace NodoAme;

public class SongCast{
	[JsonPropertyName("id")]
	public string? Id {get;set;}

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("songSoft")]
	public string? SongSoft { get; set; }

	[JsonPropertyName("lyricsMode")]
	public ExportLyricsMode LyricsMode { get; set; }

	[JsonPropertyName("exportFile")]
	public NodoAme.Models.ExportFileType ExportFile { get; set; }

	[JsonPropertyName("hasEmotion")]
	public bool? HasEmotion { get; set; }

	[JsonPropertyName("noteSplitMode")]
	public NodoAme.Models.NoteSplitModes? NoteSplitMode { get; set; }

	/// <summary>
	/// voice lib's character name text for VoiSona
	/// </summary>
	[JsonPropertyName("charaNameAsAlphabet")]
	public string? CharaNameAsAlphabet { get; set; }

	/// <summary>
	/// Voice lib version number text.
	/// </summary>
	[JsonPropertyName("voiceVersion")]
	public string? VoiceVersion { get; set; }
}

public static class SongSoftName{
	public static string CEVIO_AI = "CeVIO AI";
	public static string CEVIO_CS = "CeVIO CS";

	[Obsolete("use VOISONA")]
	public static string CEVIO_Pro = "CeVIO Pro";

	public static string VOISONA = "VoiSona";
}

public class SongVoiceStyleParam{
	public string? Id { get; set; }
	public string? Name { get; set; }
	public double Min { get; set; }
	public double Max { get; set; }
	public double DefaultValue { get; set; }
	public double? SmallChange { get; set; }
	public double Value { get; set; }
}
