using System.Reflection.Emit;
using Xunit;
using NodoAme;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Xunit.Abstractions;

namespace test;

public class WrapperTest
{
	private readonly ITestOutputHelper output;

	public WrapperTest(ITestOutputHelper output)
	{
		this.output = output;
	}

	[Fact]
	public void Test1()
	{
		Assert.True(true);
	}

	[Theory]
	[InlineData(10,150,2880)]
	public void GetParameterLength(
		double serifLen,
		double tempo,
		double expect = 0.0
	){
		var result = Wrapper.GetParametersLength(serifLen, tempo);

		Assert.InRange(tempo, 1, 1000);
		Assert.Equal(expect, result);
	}
}