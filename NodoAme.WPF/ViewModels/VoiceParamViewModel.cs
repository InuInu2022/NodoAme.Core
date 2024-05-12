using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Epoxy;

namespace NodoAme.ViewModels;

/// <summary>
/// パラメータUI表示用VMクラス
/// </summary>
/// <remarks>
/// JSONシリアライズクラスをそのままBindingすると値の反映がViewに反映されないのでわざわざ経由させる
/// </remarks>
[ViewModel]
public record VoiceParamViewModel
{
	public SongVoiceStyleParam? Source {get;set;}
	public string? Name { get; set; }
	public double Value { get; set; }
	public double? DefaultValue { get; set; }
	public double Min { get; set; }
	public double Max { get; set; }
	public string? Id { get; set; }
	public double? SmallChange { get; set; }
	public Version? Version { get; set; }

	[PropertyChanged(nameof(Value))]
	[SuppressMessage("Usage", "IDE0051")]
	private async ValueTask ValueChangedAsync(double value){
		if(Source is not null)
		{
			Source.Value = value;
		}

		await Task.CompletedTask
			.ConfigureAwait(false);
	}
}