using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Tasks;
using NodoAme.ViewModels;

namespace NodoAme.Models;

/// <summary>
/// A simple POCO class for configuration json
/// </summary>
public class UserSettings
{
	[HideForUser]
	public string Version { get; } = "0.3.0";

	public int DefaultSerifLines { get; set; } = 30;    //初期表示セリフ行
	public string? PathToSaveDirectory { get; set; } = "./out/";
	public ExportLyricsMode SongExportLyricsMode { get; set; } = ExportLyricsMode.PHONEME;

	#region export_serif_text_options

	public bool IsExportSerifText { get; set; } = false;
	public string PathToExportSerifTextDir { get; set; } = "./out/";
	public string DefaultExportSerifTextFileName { get; set; } = SERIF_FILE_NAME;

	public ObservableCollection<SongSoftTracFileExtSetting> ExportFileExtentions
		= new()
		{
			new SongSoftTracFileExtSetting{SongSoft=SongSoftName.CEVIO_AI, FileExt="ccst"},
			new SongSoftTracFileExtSetting{SongSoft=SongSoftName.CEVIO_CS, FileExt="ccst"}
		};

	public const string SERIF_FILE_NAME = "$セリフ$.txt";

	public bool IsOpenCeVIOWhenExport { get; set; } = true;
	public bool IsExportAsTrac { get; set; } = true;

	#endregion export_serif_text_options

	#region display_phonemes_options

	public bool IsUseSeparaterSpace { get; set; } = true;
	public bool IsConvertToHiragana { get; set; } = false;
	public bool IsConvertToPhoneme { get; set; } = true;

	public bool IsCheckJapaneseSyllabicNasal { get; set; }
	public bool IsCheckJananeseNasalGa { get; set; } = false;

	public VowelOptions VowelOption { get; set; } = VowelOptions.DoNothing;
	public bool IsCheckJapaneseRemoveNonSoundVowel { get; set; } = false;
	public bool IsCheckJapaneseSmallVowel { get; set; } = false;

	#endregion display_phonemes_options

	public NoteAdaptMode AdaptingNoteToPitchMode { get; set; } = NoteAdaptMode.MEDIAN;

	public NoteSplitModes NoteSplitMode { get; set; } = NoteSplitModes.SPLIT_ONLY;

	public BreathSuppressMode BreathSuppress { get; set; } = BreathSuppressMode.NO_BREATH;

	[JsonIgnore]
	public const string UserSettingsFileName = "usersettings.json";
	[JsonIgnore]
	public static string UserSettingsPath
		= $"{Directory.GetCurrentDirectory()}/{UserSettings.UserSettingsFileName}";

	private static readonly JsonSerializerOptions JsonOption
		= new()
		{
			Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
			WriteIndented = true
		};

	public void CreateFile(string path){
		var s = JsonSerializer.Serialize(this, JsonOption);
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
		writer.WriteLine(s);
		writer.Close();
	}

	public async ValueTask SaveAsync(){
		if(this is null){return;}

		var s = JsonSerializer.Serialize(this, JsonOption);
		using var writer = new StreamWriter(UserSettingsPath, false, Encoding.UTF8);
		await writer.WriteLineAsync(s);
		writer.Close();
	}
}

[AttributeUsage(AttributeTargets.Property)]
public class HideForUserAttribute : Attribute{}
