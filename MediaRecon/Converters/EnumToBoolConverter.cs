using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;

namespace ApexBytez.MediaRecon.Converters
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="https://riptutorial.com/wpf/example/22575/markup-extension-used-with-ivalueconverter"/>
    /// https://stackoverflow.com/questions/9212873/binding-radiobuttons-group-to-a-property-in-wpf
    internal class EnumToBoolConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            // Do the conversion from fileSize to string format
            return value.Equals(parameter);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <seealso cref="https://stackoverflow.com/questions/2144441/wpf-one-way-ivalueconverter"/>
        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return ((bool)value) ? parameter : Binding.DoNothing;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

}
