using System;
using System.Globalization;
using System.Windows.Data;
using Epoxy;

namespace NodoAme.Models
{
	public enum VowelOptions
	{
		/// <summary>
		/// 何もしない
		/// </summary>
		DoNothing,
		/// <summary>
		/// 小文字音素表記に変換
		/// </summary>
		Small,
		/// <summary>
		/// 無声母音 U,I を削除
		/// </summary>
		Remove,
	}
	/*
	public sealed class VowelOptionConverter : ValueConverter<VowelOptions,string,bool>
	{
		public override bool TryConvert(VowelOptions option, string param, out bool result)
		{
			
			
			if(param == null){
				result = false;
				return false;
			}

			result = option.ToString() == param;
			return true;
		}
	}*/


	public class VowelOptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
			if (!(parameter is string ParameterString))
			{
				return System.Windows.DependencyProperty.UnsetValue;
			}

			if (!Enum.IsDefined(value.GetType(), value))
            {
                return System.Windows.DependencyProperty.UnsetValue;
            }

            object paramvalue = Enum.Parse(value.GetType(), ParameterString);

            return (int)paramvalue == (int)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
			return !(parameter is string ParameterString) ? System.Windows.DependencyProperty.UnsetValue : Enum.Parse(targetType, ParameterString);
		}
    }

	[ValueConversion(typeof(Enum), typeof(bool))]
    public class RadioButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Binding.DoNothing;
            if ((bool)value)
            {
                return Enum.Parse(targetType, parameter.ToString());
            }
            return Binding.DoNothing;
        }
    }
}
