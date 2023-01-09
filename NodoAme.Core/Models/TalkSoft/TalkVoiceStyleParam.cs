namespace NodoAme;

/// <summary>
/// 感情合成用
/// </summary>
public class TalkVoiceStyleParam{
	public string? Id { get; set; }
	public string? Name { get; set; }
	public double Min { get; set; }
	public double Max { get; set; }
	public double DefaultValue { get; set; }
	public double? SmallChange { get; set; }
	public double Value { get; set; }
}