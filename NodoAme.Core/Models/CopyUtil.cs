using System.Text.Json;

namespace NodoAme.Models;

public static class CopyUtil
{
	public static T? DeepCopy<T>(T source) =>
		JsonSerializer
			.Deserialize<T>(JsonSerializer.Serialize(source));
}