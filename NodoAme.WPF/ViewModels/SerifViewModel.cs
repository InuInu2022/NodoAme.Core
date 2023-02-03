using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Epoxy;
using Epoxy.Synchronized;

namespace NodoAme.ViewModels;

[ViewModel]
public class SerifViewModel
{
	public string? SourceText { get; set; }
	public string? ConvertedText { get; set; }

	public string? SerifTime { get; set; }
	public MainWindowViewModel? ParentVM { get; set; }

	public bool EnabledSerifInput { get; set; }
	public bool EnabledPreview { get; set; }
	public bool EnabledExport { get; set; }


	public Visibility ExportProgress { get; set; } = Visibility.Collapsed;

	public Visibility PreviewProgress { get; set; } = Visibility.Collapsed;

	public Command PreviewTalk { get; set; }
	public Command ExportTrackFile { get; set; }

	public Command ExportPreviewWav { get; set; }
	public Command CheckEnterAndAddRow { get; private set; }
	private string? LastSourceText { get; set; }
	private SongCast? LastCast { get; set; }

	public SerifViewModel()
	{
		this.PreviewTalk = CommandFactory.Create<RoutedEventArgs>(
			async _ => {
				EnabledPreview = false;
				EnabledExport = false;
				PreviewProgress = Visibility.Visible;
				SerifTime = await ParentVM!.PreviewTalkFromListAsync(this.SourceText);
				EnabledPreview = true;
				EnabledExport = true;
				PreviewProgress = Visibility.Collapsed;
			}
		);

		this.ExportTrackFile = CommandFactory.Create<RoutedEventArgs>(async _ =>
		{
			EnabledExport = false;
			EnabledPreview = false;
			ExportProgress = Visibility.Visible;
			var cast = ParentVM!.ExportSongCastItems[ParentVM.ExportSongCastSelected];
			const double alpha = 0.0;	//TODO:
			await ParentVM!.ExportFileFromListAsync(
				this.SourceText,
				cast.Id,
				alpha,
				isTrack: true,
				songCast: cast
			);
			EnabledExport = true;
			EnabledPreview = true;
			ExportProgress = Visibility.Collapsed;
		});

		ExportPreviewWav =
			CommandFactory
				.Create<RoutedEventArgs>(
					async _ =>
					{
						EnabledExport = false;
						//TODO:enable loading indicater
						await ParentVM!
							.ExportPreviewWavFromListAsync(this.SourceText);
						EnabledExport = true;
					}
					);

		this.CheckEnterAndAddRow = Command.Factory.CreateSync<RoutedEventArgs>(e =>
		{
			Debug.WriteLine("e:" + e.ToString());
			var k = (KeyEventArgs)e;
			if (k.Key != Key.Enter)
			{
				return;
			}

			k.Handled = true;
		});
	}

	[PropertyChanged(nameof(SourceText))]
	private async ValueTask SourceTextChangedAsync(string sourceText)
	{
		//空文字列なら抜ける
		if (string.IsNullOrEmpty(sourceText) || string.IsNullOrWhiteSpace(sourceText)){
			EnabledExport = false;
			EnabledPreview = false;
			ConvertedText = "";
			SerifTime = "";
			return;
		}

		EnabledExport = true;
		EnabledPreview = true;

		//同じ文字列なら処理を抜ける
		if(this.LastSourceText == sourceText
			&& this.LastCast != ParentVM!.ExportSongCastItems[ParentVM.ExportSongCastSelected]) {return;}

		this.LastSourceText = sourceText;
		this.ConvertedText = await ParentVM!.ConvertFromListAsync(sourceText);
		//var s = ConvertFromList(sourceText);
		//if(IsTalkSoftComboEnabled)InitVoices();
		//return true;//new ValueTask();
	}
}
