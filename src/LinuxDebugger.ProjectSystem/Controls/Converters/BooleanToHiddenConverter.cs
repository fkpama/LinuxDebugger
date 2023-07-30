using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.VisualStudio.PlatformUI;

namespace LinuxDebugger.ProjectSystem.Controls.Converters
{
    //
    // Summary:
    //     Represents the converter that converts Boolean values to and from System.Windows.Visibility
    //     enumeration values.
    [Localizability(LocalizationCategory.NeverLocalize)]
    public sealed class BooleanToHiddenConverter : IValueConverter
    {
        static TypeConverter s_boolConverter = TypeDescriptor.GetConverter(typeof(bool));
        //
        // Summary:
        //     Converts a Boolean value to a System.Windows.Visibility enumeration value.
        //
        // Parameters:
        //   value:
        //     The Boolean value to convert. This value can be a standard Boolean value or a
        //     nullable Boolean value.
        //
        //   targetType:
        //     This parameter is not used.
        //
        //   parameter:
        //     This parameter is not used.
        //
        //   culture:
        //     This parameter is not used.
        //
        // Returns:
        //     System.Windows.Visibility.Visible if value is true; otherwise, System.Windows.Visibility.Collapsed.
        public object Convert(object value,
                              Type targetType,
                              object parameter,
                              CultureInfo culture)
        {
            bool flag = false;
            if (value is bool b)
            {
                flag = b;
            }
            else if (value is bool?)
            {
                bool? flag2 = (bool?)value;
                flag = flag2.HasValue && flag2.Value;
            }

            if (parameter is not null)
            {
                if (parameter is bool reversed && reversed)
                    flag = !flag;
                if (parameter is string s
                    && bool.TryParse(s.ToLowerInvariant(), out reversed)
                    && reversed)
                    flag = !flag;
            }
            return (!flag) ? Visibility.Hidden : Visibility.Visible;
        }

        //
        // Summary:
        //     Converts a System.Windows.Visibility enumeration value to a Boolean value.
        //
        // Parameters:
        //   value:
        //     A System.Windows.Visibility enumeration value.
        //
        //   targetType:
        //     This parameter is not used.
        //
        //   parameter:
        //     This parameter is not used.
        //
        //   culture:
        //     This parameter is not used.
        //
        // Returns:
        //     true if value is System.Windows.Visibility.Visible; otherwise, false.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v ? v == Visibility.Visible : Boxes.BooleanFalse;
        }

        //
        // Summary:
        //     Initializes a new instance of the System.Windows.Controls.BooleanToVisibilityConverter
        //     class.
        public BooleanToHiddenConverter()
        {
        }
    }
}
