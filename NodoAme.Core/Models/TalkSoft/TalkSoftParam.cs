using System.Text.Json.Serialization;

namespace NodoAme;

/// <summary>
/// トークソフトの基本パラメータ
/// ソフト毎にパラメータを変えられる
/// </summary>
public class TalkSoftParam{
	[JsonPropertyName("id")]
	public string? Id {get;set;}

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("min")]
	public double Min { get; set; }

	[JsonPropertyName("max")]
	public double Max { get;set;}

	[JsonPropertyName("defaultValue")]
	public double? DefaultValue { get; set; }

	[JsonPropertyName("smallChange")]
	public double? SmallChange { get; set; }

	public double? Value { get; set; }
}