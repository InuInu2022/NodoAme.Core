using System;
using System.Windows;

namespace NodoAme.Models;

public static class MessageDialog
{
	public static void Show(
		string message,
		string caption,
		MessageDialogType dialogType
	){
		var type = dialogType switch
		{
			MessageDialogType.Error => MessageBoxImage.Error,
			_ => MessageBoxImage.None
		};
		MessageBox.Show(
			message,
			caption,
			MessageBoxButton.OK,
			type
		);
	}
}
