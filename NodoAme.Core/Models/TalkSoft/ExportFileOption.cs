using System;
using System.Collections.ObjectModel;

namespace NodoAme.Models;

/// <summary>
/// ファイル書き出しオプション
/// </summary>
public record ExportFileOption
{
	public ExportFileOption(
		string serifText,
		string castId
	)
	{
		SerifText = serifText;
		CastId = castId;
	}

	public string SerifText { get; set; }
	public string CastId { get; set; }
	public double Alpha { get; set; }
	public bool IsExportAsTrack { get; set; } = true;
	public bool IsOpenCeVIO { get; set; } = false;
	public string ExportPath { get; set; } = "";
	public ExportLyricsMode ExportMode { get; set; }
		= ExportLyricsMode.KANA;
	public SongCast? Cast { get; set; } = null;
	public NoteAdaptMode NoteAdaptMode { get; set; }
		= NoteAdaptMode.FIXED;
	public NoteSplitModes NoteSplitMode { get; set; }
		= NoteSplitModes.IGNORE_NOSOUND;
	public ExportFileType FileType { get; set; }
		= ExportFileType.CCS;
	public BreathSuppressMode BreathSuppress { get; set; }
		= BreathSuppressMode.NONE;
	public ObservableCollection<SongVoiceStyleParam>? SongVoiceStyles { get; set; }
		= null;
	public NoPitchModes NoPitch { get; set; }
		= NoPitchModes.REMOVE;
	public NoSoundVowelsModes NoSoundVowelsModes { get; set; }
		= NoSoundVowelsModes.VOLUME;
	public ScoreDynamics Dynamics { get; set; }
		= ScoreDynamics.N;
	public double Tempo { get; set; } = 150;
	public string SoundFilePath { get; set; } = "";
	public string LabelFilePath { get; set; } = "";
}