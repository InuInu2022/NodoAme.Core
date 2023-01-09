#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

using Microsoft.Win32;

using MathNet.Numerics.Statistics;

using NAudio.Wave;

using NLog;

using NodoAme.Models;

using SharpOpenJTalk;

using Tssproj;

using WanaKanaNet;

namespace NodoAme
{

	/// <summary>
	/// トークエンジン列挙
	/// </summary>
	public static class TalkEngine{
		public const string CEVIO = "CeVIO";
		public const string OPENJTALK = "OpenJTalk";
		public const string VOICEVOX = "VOICEVOX";
	}
}