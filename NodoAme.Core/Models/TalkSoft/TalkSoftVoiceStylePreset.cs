using System.Text.Json.Serialization;

namespace NodoAme;

public class TalkSoftVoiceStylePreset{
	[JsonPropertyName("id")]
	public string? Id {get;set;}

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	public uint? Value { get; set; }

	[JsonPropertyName("path")]
	public string? Path {get;set;}
}