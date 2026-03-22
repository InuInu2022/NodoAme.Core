using System.Text.Json.Serialization;

namespace NodoAme.Models;

public class JapaneseRule
{
	[JsonPropertyName("rules")]
	public ConvertRule? Rules {get;set;}
}

public class ConvertRule{
	[JsonPropertyName("p2k")]
	public dynamic? PhenomeToKana { get; set; }
}
