using System;
using System.Collections.Generic;
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

	[JsonPropertyName("songExportPreset")]
	public IList<SongExportPresetCast>? SongExportPreset { get; set; }
}

public static class SongSoftName{
	public static string CEVIO_AI = "CeVIO AI";
	public static string CEVIO_CS = "CeVIO CS";

	[Obsolete("use VOISONA")]
	public static string CEVIO_Pro = "CeVIO Pro";

	public static string VOISONA = "VoiSona";
}

public record SongVoiceStyleParam{
	[JsonPropertyName("id")]
	public string? Id { get; set; }

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("min")]
	public double Min { get; set; }

	[JsonPropertyName("max")]
	public double Max { get; set; }

	[JsonPropertyName("defaultValue")]
	public double DefaultValue { get; set; }

	[JsonPropertyName("smallChange")]
	public double? SmallChange { get; set; }

	[JsonPropertyName("version")]
	public Version? Version { get; set; }

	public double Value { get; set; }
}

public enum SongExportPresets
{
	/// <summary>
	/// 何もしない
	/// </summary>
	NONE = 0,

	/// <summary>
	/// ささやき
	/// </summary>
	WHISPER = 1
}

/// <summary>
/// <see cref="SongExportPresets"/>選択時の全体共通設定
/// </summary>
public record SongExportPresetCommon{
	[JsonPropertyName("id")]
	public SongExportPresets Id { get; set; }

	/// <summary>
	/// トラック全体のDynamics指定
	/// </summary>
	[JsonPropertyName("trackDynamics")]
	public Models.ScoreDynamics TrackDynamics { get; set; }
}

/// <summary>
/// <see cref="SongExportPresets"/>選択時のキャストごとの設定
/// </summary>
public record SongExportPresetCast{
	[JsonPropertyName("id")]
	public SongExportPresets Id { get; set; }

	/// <summary>
	/// special label as "※", "＠”, etc...
	/// </summary>
	[JsonPropertyName("specialLabel")]
	public string? SpecialLabel { get; set; }

	/// <summary>
	/// Clipping volume limit for <see cref="SongExportPresets.WHISPER"/>
	/// </summary>
	[JsonPropertyName("clipVol")]
	public double? ClipVol { get; set; }

	[JsonPropertyName("noteDynamics")]
	public Models.ScoreDynamics NoteDynamics { get; set; }

	/// <summary>
	/// トラック全体のAlpha指定
	/// </summary>
	[JsonPropertyName("trackAlp")]
	public double? TrackAlpha { get; set; }

	/// <summary>
	/// トラック全体のHusky指定
	/// </summary>
	[JsonPropertyName("trackHus")]
	public double? TrackHusky { get; set; }

	/// <summary>
	/// トラック全体のEmotion指定
	/// </summary>
	[JsonPropertyName("trackEmo")]
	public double? TrackEmotion { get; set; }

	/// <summary>
	/// トラック全体のTune指定
	/// </summary>
	[JsonPropertyName("trackTune")]
	public double? TrackTune { get; set; }

	/// <summary>
	/// トラック全体のPitchTune指定
	/// </summary>
	[JsonPropertyName("trackPit")]
	public double? TrackPitch { get; set; }
}
