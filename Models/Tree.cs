using System;
using System.Collections.Generic;
using System.Linq;

namespace Tssproj;

public abstract class TssCommon{
	protected const byte NULL_END = 0x00;
}

public class TssTree: TssCommon
{
	//const byte NULL_END = 0x00;
	public string Name { get; set; }
	public int Count {
		get => this.Children?.Count ?? 0;
	}
	public List<TssTree> Children = new();


    public int AttributeCount {
		get => this.Attributes?.Count ?? 0;
	}
	public List<TssKeyValue> Attributes = new();

	public TssTree(
        string Name
    )
	{
		this.Name = Name;
	}

    public byte[] GetChildHeader(bool withNull = true){
		//if(Count<=0) return new byte[1] { NULL_END };

		if(withNull){
			return new byte[3]{
				00,
				01,
				Convert.ToByte(Count)
			};
		}else{
			return new byte[2]{
				01,
				Convert.ToByte(Count)
			};
		}
		
	}

    public byte[] GetAttributeHeader(){
        if(AttributeCount<=0) return new byte[1] { NULL_END };

		return new byte[2]{
			//00,
			01,
            Convert.ToByte(AttributeCount)
		};
	}

    public byte[] GetBytes(bool withNull = true, bool endNull = true){
        var hexName = System.Text.Encoding.UTF8.GetBytes(Name);
		var atHead = GetAttributeHeader();

		var atValues = AttributeCount > 0
			? Attributes
			.Select(v => v.GetBytes())
			.Aggregate((a, b) => a.Concat(b).ToArray())

			: Array.Empty<byte>()
	    	;
		if(Name=="Timing" || Name=="LogF0" || Name=="C0"){
			//強制的に
			withNull = false;
			endNull = false;
		}
		var cldHead = GetChildHeader(withNull);
        //Console.WriteLine(BitConverter.ToString(cldHead));
		var cldValues = Count > 0
			? Children
				.Select(v => v.GetBytes())
				.Aggregate((a, b) => a.Concat(b).ToArray())
			: Array.Empty<byte>()
		    ;


		var ret = hexName.Append(NULL_END);

		if(AttributeCount>0){
			ret = ret
				.Concat(atHead)
				.Concat(atValues)
				.ToArray();
		}
        if(Count>0){
			ret = ret
				.Concat(cldHead)
				.Concat(cldValues)
				.ToArray();
		}

		return endNull ? ret.Append(NULL_END).ToArray() : ret.ToArray();
	}

    public void AddAttribute(string key, dynamic value){
		Attributes.Add(new TssKeyValue(key, value));
	}
}

public class TssKeyValue : TssCommon
{
	public string Key { get; set; }
	public dynamic Value { get; set; }

	public TssKeyValue(
		string key,
		dynamic value)
	{
		Key = key;
		Value = value;
	}

	public byte[] GetHeaderBytes(bool withNull, bool withData = false)
	{
		(int num, TssValueType type, byte[] data) ret = HeaderUtil.Common(Value!);

		if (withNull)
		{
			var r = new byte[4]{
				00,
				01,
				Convert.ToByte(ret.num+1),
				(byte)ret.type
			};

			return withData ? r.Concat(ret.data).ToArray() : r;
		}
		else
		{
			var r = new byte[3]{
				01,
				Convert.ToByte(ret.num+1),
				(byte)ret.type
			};

            return withData ? r.Concat(ret.data).ToArray() : r;
		}
	}

    public byte[] GetBytes(){
		var hexKey = System.Text.Encoding.UTF8.GetBytes(Key).AsSpan();
		var head = this.GetHeaderBytes(false, true);
		return hexKey.ToArray().Append(NULL_END).Concat(head).ToArray();
	}


}

public class Note : TssTree
{
    public new static int Count { get => 0; }

    /*
        Clock="3840"
		Duration="960"
		Lyric="ド"
		PitchOctave="4"
		PitchStep="0"
    */
	public Note(
        int clock,
        int duration,
        string lyrics,
        string phoneme,
        int pitchOctave,
        int pitchStep
    ) : base("Note")
	{
        //Attributes.Add(new TssKeyValue("Clock", clock) );
		AddAttribute("Clock", clock);
		AddAttribute("Duration", duration);
        AddAttribute("PitchStep", pitchStep);
        AddAttribute("PitchOctave", pitchOctave);
        AddAttribute("Lyric", lyrics);
		AddAttribute("Syllabic", 0);
		//AddAttribute("DoReMi", false);
        AddAttribute("Phoneme", phoneme);
	}
}

public abstract class AbstTreeHasDataChild: TssTree{
	protected AbstTreeHasDataChild(string Name) : base(Name)
	{
	}

	public void AddData(double value, int? index = null){
		this.Children.Add(new Data(value, index));
	}
}

public class Timing : AbstTreeHasDataChild
{
	/// <summary>
	/// Timing
	/// </summary>
	/// <param name="phonemeLen">音素数。pau/sil/clも含んだ音素の総数。</param>
	/// <param name="timeData"></param>
	public Timing(
		int phonemeLen, 
		List<Data>? timeData = null
	) : base("Timing")
	{
		AddAttribute("Length", phonemeLen);
		if(timeData is not null)this.Children = timeData.Cast<TssTree>().ToList();
	}
}

public class Pitch : AbstTreeHasDataChild
{
	public Pitch(
		int length,
		List<Data>? pitchData = null
	) : base("LogF0")
	{
		AddAttribute("Length", length);
		if(pitchData is not null)this.Children = pitchData.Cast<TssTree>().ToList();
	}
}

public class Volume : AbstTreeHasDataChild
{
	public Volume(
		int length,
		List<Data>? volumeData = null
	) : base("C0")
	{
		AddAttribute("Length", length);
		if(volumeData is not null)this.Children = volumeData.Cast<TssTree>().ToList();
	}
}

public class Data : TssTree
{
	public Data(double value, int? index = null) : base("Data")
	{
		if(index is not null && index >= 0){
			AddAttribute("Index", index);
		}
		AddAttribute("Value", value);
	}
}

public static class HeaderUtil{
	public static (int num, TssValueType type, byte[] data) Common(dynamic value)
	{
		if (typeof(int) == value.GetType())
		{
			var b = BitConverter.GetBytes((int)value);
			return (b.Length, TssValueType.Int32, b);
		}
        else if(typeof(bool) == value.GetType()){
            var b = BitConverter.GetBytes((bool)value);
			return (b.Length, TssValueType.Bool, b);
        }
		else if (typeof(double) == value.GetType())
		{
			var b = BitConverter.GetBytes((double)value);
			return (b.Length, TssValueType.Double, b);
		}
		else if (typeof(string) == value.GetType())
		{
			byte[] b = System.Text.Encoding.UTF8.GetBytes(value);
			return (b.Length+1, TssValueType.String, b.Append<byte>(0).ToArray());
		}
		else
		{
			return (0, TssValueType.Unknown, Array.Empty<byte>());
		}
	}
}