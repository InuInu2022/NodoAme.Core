using System.IO;
using System.Linq;

using NodoAme.Models;

namespace NodoAme.Core.Services;

public static class FileNameService
{
	static readonly char[] InvalidChars =
	[
		.. Path.GetInvalidFileNameChars(),
		.. Path.GetInvalidPathChars(),
	];

	/// <summary>
	/// 安全な名前を得る
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public static string GetSafeName(string name)
	{
		return string.Concat(name.Where(c => !InvalidChars.Contains(c)));
	}

	/// <summary>
	/// 安全なファイル名を得る
	/// </summary>
	/// <param name="serifText"></param>
	/// <param name="pathInfo"></param>
	/// <returns></returns>
	public static string GetSafeFileName(
		string serifText,
		(string Dir, string Ext) pathInfo,
		ExportFileOption option)
	{
		const int maxPath = 260;
		const int offset = 10;
		var fileName = GetSafeName(serifText);
		var combinedPath = string.Empty;

		try
		{
			combinedPath = Path.GetFullPath(
				Path.Combine(pathInfo.Dir, $"{fileName}.{pathInfo.Ext}")
			);
		}
		catch (PathTooLongException)
		{
			if(option.IsUseShortFileName)
			{
				fileName = "track";
			}
			else
			{
				//パスが長すぎる場合は、ファイル名を短くして再度結合を試みる
				var excessLength =
					(
						Path.GetFullPath(pathInfo.Dir).Length
						+ Path.DirectorySeparatorChar.ToString().Length
						+ fileName.Length
						+ 1 //ドット
						+ pathInfo.Ext.Length
					)
					- maxPath
					+ offset;
				if (excessLength < fileName.Length)
				{
					fileName = fileName[..^excessLength];
				}
				else
				{
					fileName = fileName[..1]; //最低限1文字は残す
				}
			}
		}

		//同名ファイルチェック
		if(option.IsOverrideSameName){
			ExportMapService.Add($"{fileName}.{pathInfo.Ext}", serifText);
			return fileName;
		}
		if (FileExists(pathInfo, fileName))
		{
			var i = option.IsUseShortFileName ? 1 : 2;
			string newFileName;
			do
			{
				newFileName = $"{fileName}_{i:D3}";
				i++;
			} while (FileExists(pathInfo, newFileName));
			ExportMapService.Add($"{newFileName}.{pathInfo.Ext}", serifText);
			return newFileName;
		}
		ExportMapService.Add($"{fileName}.{pathInfo.Ext}", serifText);
		return fileName;

		static bool FileExists((string Dir, string Ext) pathInfo, string fileName) =>
			File.Exists(Path.GetFullPath(Path.Combine(pathInfo.Dir, $"{fileName}.{pathInfo.Ext}")));
	}
}
