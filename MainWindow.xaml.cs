using System.Windows;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace NodoAme
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		static public Logger Logger = LogManager.GetCurrentClassLogger();
		public MainWindow()
		{
			InitializeComponent();
			InitLogger();
			Logger.Info("NodoAme initialized.");
		}

		private static void InitLogger()
		{
			var config = new LoggingConfiguration();

			var fileTarget = new FileTarget();
			config.AddTarget("file", fileTarget);

			fileTarget.Name = "f";
			fileTarget.FileName = "${basedir}/logs/${shortdate}.log";
			fileTarget.Layout = "${longdate} [${uppercase:${level}}] ${message}";

			var rule1 = new LoggingRule("*", LogLevel.Info, fileTarget);
			config.LoggingRules.Add(rule1);

			LogManager.Configuration = config;
		}
	}
}
