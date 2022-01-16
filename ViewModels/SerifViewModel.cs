using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Epoxy;
using Epoxy.Synchronized;

namespace NodoAme.ViewModels
{
	[ViewModel]
	public class SerifViewModel
	{
		public string? SourceText { get; set; }
		public string? ConvertedText { get; set; }
		public MainWindowViewModel? ParentVM { get; set; }

		public bool EnabledPreview { get; set; }
		public bool EnabledExport { get; set; }

		public Visibility ExportProgress { get; set; } = Visibility.Collapsed;

		public Visibility PreviewProgress { get; set; } = Visibility.Collapsed;

		public Command PreviewTalk { get; set; }
		public Command ExportTrackFile { get; set; }

		public Command ExportPreviewWav { get; set; }
		public Command CheckEnterAndAddRow { get; private set; }

		public SerifViewModel()
		{

			this.PreviewTalk = CommandFactory.Create<RoutedEventArgs>(
				async _ => {
					EnabledPreview = false;
					PreviewProgress = Visibility.Visible;
					await ParentVM!.PreviewTalkFromList(this.SourceText);
					EnabledPreview = true;
					PreviewProgress = Visibility.Collapsed;
				}
			);

			this.ExportTrackFile = CommandFactory.Create<RoutedEventArgs>(async _ =>
			{
				EnabledExport = false;
				ExportProgress = Visibility.Visible;
				var cast = ParentVM!.ExportCastItems[ParentVM.ExportCastSelected];
				var alpha = 0.0;//TODO:
				await ParentVM!.ExportFileFromList(
					this.SourceText,
					cast.Id,
					alpha,
					isTrack: true);
				EnabledExport = true;
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
							.ExportPreviewWavFromList(this.SourceText);
						EnabledExport = true;
					}
				);

			this.CheckEnterAndAddRow = Command.Factory.CreateSync<RoutedEventArgs>(e =>
				{
					Debug.WriteLine("e:" + e.ToString());
					var k = (KeyEventArgs)e;
					if (k.Key == Key.Enter)
					{
						k.Handled = true;

					}
				});

		}




		[PropertyChanged(nameof(SourceText))]
		private async ValueTask SourceTextChangedAsync(string sourceText)
		{
			if (sourceText?.Length == 0) return; //false;new ValueTask();    //抜ける
			this.ConvertedText = await ParentVM!.ConvertFromListAsync(sourceText);
			//var s = ConvertFromList(sourceText);
			//if(IsTalkSoftComboEnabled)InitVoices();
			//return true;//new ValueTask();
		}
	}
}
