using System.Collections.Generic;
using System.Diagnostics;

namespace NodoAme.Core.Services;

public static class ExportMapService
{
	// track_00x - original file name
	static readonly Dictionary<string, string> ExportMap = [];

	public static void Add(
		string shortName,
		string originalName
	)
	{
		ExportMap[shortName] = originalName;
	}

	public static void Export(string path)
	{
		foreach (var kvp in ExportMap)
		{
			var shortName = kvp.Key;
			var originalName = kvp.Value;
			Debug.WriteLine($"Exporting {shortName} as {originalName}");
		}
	}

	public static void Clear()
	{
		ExportMap.Clear();
	}
}