using System.Reflection.Emit;
using Xunit;
using NodoAme;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Xunit.Abstractions;

namespace test;

public class UnitTest1
{
	private readonly ITestOutputHelper output;

	public UnitTest1(ITestOutputHelper output)
	{
		this.output = output;
	}

	[Fact]
	public void Test1()
	{
		Assert.True(true);
	}

	[Fact]
	public async void SplitPhonemesToNotesAsync(){
		List<NodoAme.Models.Label> phs = new()
			{
				new ("sil",0,1),
				new ("k",1,2),
				new ("o",2,3),
				new ("N",3,4),
				new ("n",4,5),
				new ("i",5,6),
				new ("ch",6,7),
				new ("i",7,8),
				new ("w",8,9),
				new ("a",9,10),
				new ("pau",10,11),
				new ("a",11,12),
				new ("a",12,13),
				new ("sil",13,14),
				new ("n",14,15),
				new ("cl",15,16),
				new ("t",16,17),
				new ("e",17,18),
				new ("sil",18,19),
			};

		var (notesList, phNum) = await ProjectWriter.SplitPhonemesToNotesAsync(
			phs,
			ExportLyricsMode.PHONEME,
			NodoAme.Models.NoteSplitModes.SYLLABLE
		);

		Assert.True(phNum > 0);
		Assert.NotNull(notesList);

		//Assert.True(notesList.Count == 3, $"should be 5, {notesList.Count}");
		var o = "";
		foreach(var s in notesList){
			var a = (List<dynamic>)s;
			if(a is null)continue;

			o += string.Join(",", a.Select(v => v.Phoneme).ToArray()) + "\n";
		}
		output.WriteLine($"notesList: {o}");

		//Assert.True();
	}
}