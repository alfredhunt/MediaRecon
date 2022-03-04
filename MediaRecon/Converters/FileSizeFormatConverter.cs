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
    internal class FileSizeFormatConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            // Do the conversion from fileSize to string format
            if (value is long)
            {
                return ((long)value).FormatFileSize();
            }
            else
            {
                throw new NotSupportedException("Value must be a long");
            }
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
            throw new NotSupportedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

}
