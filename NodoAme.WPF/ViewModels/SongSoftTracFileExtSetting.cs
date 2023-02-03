using System.Threading.Tasks;
using Epoxy;
using NodoAme.Models;

namespace NodoAme.ViewModels
{
	[ViewModel]
	public class SongSoftTracFileExtSetting{
		public string? SongSoft { get; set; }
		public string? FileExt { get; set; } = "ccst";
	}
}
