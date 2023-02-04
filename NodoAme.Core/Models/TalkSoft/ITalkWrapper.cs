using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using NodoAme.Models;

namespace NodoAme;

public interface ITalkWrapper
{
	TalkSoftVoice? TalkVoice { get; set; }
	TalkSoftVoiceStylePreset? VoiceStyle { get; set; }
	IList<TalkVoiceStyleParam>? VoiceStyleParams { get; set; }
	TalkSoft TalkSoft { get; set; }
	bool IsActive { get; set; }

	ValueTask<bool> ExportFileAsync(
		string serifText,
		string castId,
		double alpha,
		bool isExportAsTrack = true,
		bool isOpenCeVIO = false,
		string exportPath = "",
		ExportLyricsMode exportMode = ExportLyricsMode.KANA,
		SongCast? cast = null,
		NoteAdaptMode noteAdaptMode = NoteAdaptMode.FIXED,
		NoteSplitModes noteSplitMode = NoteSplitModes.IGNORE_NOSOUND,
		ExportFileType fileType = ExportFileType.CCS,
		BreathSuppressMode breathSuppress = BreathSuppressMode.NONE,
		ObservableCollection<SongVoiceStyleParam>? songVoiceStyles = null,
		NoPitchModes noPitch = NoPitchModes.REMOVE,
		NoSoundVowelsModes noSoundVowelsModes = NoSoundVowelsModes.VOLUME
	);

	ValueTask<bool> ExportSerifTextFileAsync(
		string serifText,
		string exportPath,
		string fileNamePattern,
		string SongCastName);

	ValueTask<bool> ExportSpecialFileAsync(
		SongCast cast,
		bool isExportAsTrack = true,
		bool isOpenCeVIO = false,
		string exportPath = "",
		ExportFileType fileType = ExportFileType.CCS,
		ExportLyricsMode exportMode = ExportLyricsMode.KANA);

	ObservableCollection<TalkSoftVoice>? GetAvailableCasts();
	ValueTask<IList<string>> GetLabelsAsync(string sourceText);
	ObservableCollection<TalkSoftVoiceStylePreset> GetStylePresets();
	ObservableCollection<TalkVoiceStyleParam> GetVoiceStyles();
	ValueTask PreviewSaveAsync(string serifText);
	void SetVoiceStyle(bool usePreset = true);
	ValueTask<string> SpeakAsync(string text, bool withSave = false);
}
