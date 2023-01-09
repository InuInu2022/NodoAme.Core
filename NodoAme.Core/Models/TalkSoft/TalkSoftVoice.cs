using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NodoAme;

public class TalkSoftVoice{
	[JsonPropertyName("id")]
	public string? Id {get;set;}

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("path")]
	public string? Path {get;set;}

	[JsonPropertyName("styles")]
	public IList<TalkSoftVoiceStylePreset>? Styles { get; set; }

	/// <summary>
	/// 現在の感情合成値
	/// </summary>
	public IList<TalkVoiceStyleParam>? CurrentStyleParams { get; set; }
}