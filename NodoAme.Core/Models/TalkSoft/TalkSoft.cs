using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NodoAme;

/// <summary>
/// トークソフトのデータを表現する
/// </summary>
public class TalkSoft
{
	[JsonPropertyName("id")]
	public string? Id { get; set; }

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	/// <summary>
	/// TTSのDropDown listから非表示にします
	/// </summary>
	[JsonPropertyName("hidden")]
	public bool? Hidden { get; set; }

	[JsonPropertyName("enabledPreview")]
	public bool? EnabledPreview { get; set; }

	[JsonPropertyName("enabledExport")]
	public bool? EnabledExport { get; set; }

	[JsonPropertyName("voices")]
	public IList<TalkSoftVoice>? TalkSoftVoices { get; set; }

	[JsonPropertyName("interface")]
	public TalkSoftInterface? Interface { get; set; }

	[JsonPropertyName("dic")]
	public string? DicPath { get; set; }

	[JsonPropertyName("voiceParam")]
	public IList<TalkSoftParam>? TalkSoftParams { get; set; }

	[JsonPropertyName("sampleRate")]
	public int SampleRate { get; set; } = 48000;
}