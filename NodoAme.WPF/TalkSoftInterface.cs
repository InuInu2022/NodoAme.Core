using System.Text.Json.Serialization;

namespace NodoAme;

public class TalkSoftInterface{
	[JsonPropertyName("type")]
	public string? Type {get;set;}

	[JsonPropertyName("engine")]
	public string? Engine {get;set;}

	[JsonPropertyName("env_prog")]
	public string? EnvironmentProgramVar { get; set; }

	[JsonPropertyName("dll")]
	public string? DllName { get; set; }

	[JsonPropertyName("dll_dir")]
	public string? DllPath { get; set; }

	[JsonPropertyName("service")]
	public string? Service {get;set;}

	[JsonPropertyName("talker")]
	public string? Talker {get;set;}

	[JsonPropertyName("agent")]
	public string? Agent {get;set;}

	[JsonPropertyName("restHost")]
	public string? RestHost { get; set; }
}