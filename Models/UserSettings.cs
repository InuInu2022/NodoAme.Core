using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NodoAme.Models
{
    /// <summary>
	/// A simple POCO class for configuration json
	/// </summary>
    public class UserSettings
    {
		public string Version { get; } = "0.1.0";
		public string? PathToSaveDirectory { get; set; } = "./out/";

		public bool IsUseSeparaterSpace { get; set; } = true;
		public bool IsConvertToHiragana { get; set; } = false;
		public bool IsConvertToPhoneme { get; set; } = true;

        public bool IsCheckJapaneseSyllabicNasal { get; set; }
        public bool IsCheckJananeseNasalGa { get; set; } = false;

        public VowelOptions VowelOption { get; set; } = VowelOptions.DoNothing;
        public bool IsCheckJapaneseRemoveNonSoundVowel { get; set; } = false;
        public bool IsCheckJapaneseSmallVowel { get; set; } = false;

        public bool IsOpenCeVIOWhenExport { get; set; } = true;
        public bool IsExportAsTrac { get; set; } = true;

		[JsonIgnore]
		public const string UserSettingsFileName = "usersettings.json";
        [JsonIgnore]
		public static string UserSettingsPath = $"{Directory.GetCurrentDirectory()}/{UserSettings.UserSettingsFileName}";


		public void CreateFile(string path){
			var options = new JsonSerializerOptions
			{
				WriteIndented = true
			};
			var s = JsonSerializer.Serialize(this, options);
            using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
			writer.WriteLine(s);
			writer.Close();
		}

        public async ValueTask SaveAsync(){
            var options = new JsonSerializerOptions
			{
				WriteIndented = true
			};
			var s = JsonSerializer.Serialize(this, options);
            using var writer = new StreamWriter(UserSettingsPath, false, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync(s);
			writer.Close();
        }
	}
}