using System;
using System.Text.Json.Serialization;

namespace NodoAme
{
    public class SongCast
    {
        [JsonPropertyName("id")]
		public string? Id {get;set;}
		[JsonPropertyName("name")]
		public string? Name { get; set; }
		[JsonPropertyName("songSoft")]
		public string? SongSoft { get; set; }
		[JsonPropertyName("lyricsMode")]
		public ExportLyricsMode LyricsMode { get; set; }
		[JsonPropertyName("exportFile")]
		public NodoAme.Models.ExportFileType ExportFile { get; set; }
	}

	public static class SongSoftName{
		public static string CEVIO_AI = "CeVIO AI";
		public static string CEVIO_CS = "CeVIO CS";
		public static string CEVIO_Pro = "CeVIO Pro";
	}
}
