using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CodingSeb.Localization;
using CodingSeb.Localization.Loaders;
using NLog;

namespace NodoAme;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		LocalizationLoader
			.Instance
			.FileLanguageLoaders
			.Add(new YamlFileLoader());
		LocalizationLoader
			.Instance
			.AddDirectory("dic");

		YamlMissingTranslationsLogger
			.EnableLogFor(Loc.Instance);
		//set default lang
		Loc.Instance.CurrentLanguage = "ja";

		DispatcherUnhandledException +=
			OnDispatcherUnhandledException;
		TaskScheduler.UnobservedTaskException +=
			OnUnobservedTaskException;
		AppDomain.CurrentDomain.UnhandledException +=
			OnUnhandledException;
	}

	private void OnDispatcherUnhandledException(
		object sender,
		DispatcherUnhandledExceptionEventArgs e)
	{
		var exception = e.Exception;
		HandleException(exception);
	}

	private void OnUnobservedTaskException(
		object sender,
		UnobservedTaskExceptionEventArgs e)
	{
		var exception = e.Exception.InnerException as Exception;
		HandleException(exception);
	}

	private void OnUnhandledException(
		object sender,
		UnhandledExceptionEventArgs e)
	{
		var exception = e.ExceptionObject as Exception;
		HandleException(exception);
	}

	private void HandleException(Exception? e)
	{
		var logger = LogManager.GetCurrentClassLogger();
		logger.Error($"Error!:{e?.ToString()}");
#if DEBUG
		MessageBox.Show(
			$"エラーが発生しました。\n詳細：{e?.Message}",
			"エラーが発生",
			MessageBoxButton.OK,
			MessageBoxImage.Error
		);
#endif
		//Environment.Exit(1);
	}
}
