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
	}
}
