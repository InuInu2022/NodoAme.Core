using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NodoAme;

/// <summary>
/// 出力するソングソフトのデータ
/// </summary>
public class SongSoft
{
	/// <summary>
    /// 数値。
    /// </summary>
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("voiceParam")]
	public IList<SongVoiceStyleParam>? VoiceParam { get; set; }

	[JsonPropertyName("songExportPreset")]
	public IList<SongExportPresetCommon>? SongExportPreset { get; set;}
}