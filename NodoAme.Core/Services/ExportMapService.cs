using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
		var builder = new System.Text.StringBuilder();
		builder.AppendLine("exported_name,original_name");
		foreach (var kvp in ExportMap)
		{
			var shortName = kvp.Key;
			var originalName = kvp.Value;
			Debug.WriteLine($"Exporting {shortName} as {originalName}");
			builder.AppendLine($"{shortName},{originalName}");
		}
		var p = Path.Combine(
			Path.GetDirectoryName(path),
			$"nodoame_export_map_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt"
		);
		try
		{
			File.WriteAllText(p, builder.ToString());
		}
		catch(IOException ex)
		{
			Debug.WriteLine($"Failed to write export map: {ex.Message}");
		}
	}

	public static void Clear()
	{
		ExportMap.Clear();
	}
}