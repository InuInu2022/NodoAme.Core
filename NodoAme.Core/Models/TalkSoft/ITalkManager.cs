using System;
using System.Threading.Tasks;

namespace NodoAme.Models;

public interface ITalkManager
{
	public ValueTask<double> SpeakAsync(string pathOfSerif);
}