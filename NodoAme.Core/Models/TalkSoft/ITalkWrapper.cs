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

	ValueTask<bool> ExportFileAsync(ExportFileOption option);

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
		ExportLyricsMode exportMode = ExportLyricsMode.KANA,
		bool isOpenFolder = false);

	ObservableCollection<TalkSoftVoice>? GetAvailableCasts();
	ValueTask<IList<string>> GetLabelsAsync(string sourceText);
	ObservableCollection<TalkSoftVoiceStylePreset> GetStylePresets();
	ObservableCollection<TalkVoiceStyleParam> GetVoiceStyles();
	ValueTask PreviewSaveAsync(string serifText);
	void SetVoiceStyle(bool usePreset = true);
	ValueTask<string> SpeakAsync(string text, bool withSave = false);
}
