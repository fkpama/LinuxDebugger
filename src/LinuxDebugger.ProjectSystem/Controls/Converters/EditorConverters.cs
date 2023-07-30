using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using Microsoft.VisualStudio.ProjectSystem.VS.Implementation.PropertyPages.Designer;
using Microsoft.VisualStudio.ProjectSystem.VS.PropertyPages.Designer;

namespace LinuxDebugger.ProjectSystem.Controls.Converters
{
    internal static class EditorConverters
    {
        static class PropertyCommand
        {
            private static readonly Type PropertyCommandType = Type.GetType("Microsoft.VisualStudio.ProjectSystem.VS.Implementation.PropertyPages.Designer.IPropertyCommand, Microsoft.VisualStudio.ProjectSystem.VS.Implementation");

            private static readonly MethodInfo IsCheckedMethod = PropertyCommandType.GetMethod("IsChecked", BindingFlags.Instance | BindingFlags.Public);
            private static readonly MethodInfo IsCheckableMethod = PropertyCommandType.GetMethod("IsCheckable", BindingFlags.Instance | BindingFlags.Public);
            private static readonly MethodInfo IsEnabledMethod = PropertyCommandType.GetMethod("IsEnabled", BindingFlags.Instance | BindingFlags.Public);

            public static bool IsEnabled(object obj, IProperty property)
                => (bool)IsEnabledMethod.Invoke(obj, new object[] { property });
            public static bool IsChecked(object obj, IProperty property)
                => (bool)IsCheckedMethod.Invoke(obj, new object[] { property });
            public static bool IsCheckable(object obj, IProperty property)
                => (bool)IsCheckableMethod.Invoke(obj, new object[] { property });
        }
        public static readonly IMultiValueConverter DescriptionVisibility;
        public static readonly IValueConverter InvertBoolean;
        public static readonly IMultiValueConverter PropertyMenuEnabled;
        public static readonly IMultiValueConverter PropertyMenuOpacity;
        public static readonly IMultiValueConverter PropertyConfigurationCommandChecked;
        public static readonly IMultiValueConverter PropertyCommandEnabled;
        public static readonly IMultiValueConverter PropertyConfigurationCommandIsCheckable;
        public static readonly IValueConverter ConfigurationIconButtonAccessibleName;
        public static readonly IValueConverter BooleanToHidden;
        static EditorConverters()
        {
            InvertBoolean = new LambdaConverter<bool, bool>(b => !b, b => !b);
            PropertyMenuEnabled = new LambdaMultiConverter<IProperty, bool, bool, int, bool>((property, isMouseOver, isKeyboardFocusWithin, valuesVersion) => isMouseOver | isKeyboardFocusWithin && property != null && property.IsEnabled);
            PropertyMenuOpacity = new LambdaMultiConverter<IProperty, bool, bool, int, double>((property, isMouseOver, isKeyboardFocusWithin, valuesVersion) => property == null || !property.IsEnabled || !(isMouseOver | isKeyboardFocusWithin) ? 0.0 : 1.0);
            PropertyConfigurationCommandChecked = new LambdaMultiConverter<object, IList<IPropertyValueViewModel>, int, bool>((command, values, valueCount) => valueCount != 0 && values[0].Parent != null && PropertyCommand.IsChecked(command, values[0].Parent));
            PropertyCommandEnabled = new LambdaMultiConverter<object, IProperty, int, bool>((command, property, _) => property != null && property.IsEnabled && !property.IsReadOnly && PropertyCommand.IsEnabled(command, property));
            PropertyConfigurationCommandIsCheckable = new LambdaMultiConverter<object, IProperty, int, bool>((command, property, _) => PropertyCommand.IsCheckable(command, property));
            ConfigurationIconButtonAccessibleName = new LambdaConverter<string, string>(displayName => string.Format(CultureInfo.CurrentCulture,
                Resources.ConfigurationIconButtonAccessibleName,
                displayName));
            BooleanToHidden = new BooleanToHiddenConverter();
            DescriptionVisibility = new LambdaMultiConverter<IProperty,
                string,
                int,
                Visibility>((property, description, valueCount)
                => string.IsNullOrWhiteSpace(description)
                || !(property?.Editor.ShouldShowDescription(valueCount) ?? true)
                ? Visibility.Collapsed
                : Visibility.Visible);
        }

        private sealed class LambdaMultiConverter<TFrom1, TFrom2, TFrom3, TTo> : IMultiValueConverter
        {
            private readonly Func<TFrom1?, TFrom2 ?, TFrom3 ?, TTo> convert;
            private readonly Func<TTo, (TFrom1, TFrom2, TFrom3)>? convertBack;

            public LambdaMultiConverter(
              Func<TFrom1?, TFrom2?, TFrom3?, TTo> convert,
              Func<TTo, (TFrom1, TFrom2, TFrom3)>? convertBack = null)
            {
                this.convert = convert;
                this.convertBack = convertBack;
            }

            public object? Convert(
              object[] values,
              Type targetType,
              object parameter,
              CultureInfo culture)
            {
                return values.Length == 3 &&
                    TryConvert(values[0], out TFrom1? t1) &&
                    TryConvert(values[1], out TFrom2? t2) &&
                    TryConvert(values[2], out TFrom3? t3) ?
                    this.convert(t1, t2, t3) :
                    DependencyProperty.UnsetValue;

                static bool TryConvert<T>(object o, out T? t)
                {
                    if (o is T obj)
                    {
                        t = obj;
                        return true;
                    }
                    t = default(T);
                    return o == null;
                }
            }

            public object?[] ConvertBack(
              object value,
              Type[] targetTypes,
              object parameter,
              CultureInfo culture)
            {
                if (this.convertBack != null && value is TTo to)
                {
                    var valueTuple = this.convertBack(to);
                    return new object?[3]
                    {
                        valueTuple.Item1,
                        valueTuple.Item2,
                        valueTuple.Item3
                    };
                }
                return new object?[3]
                {
                    DependencyProperty.UnsetValue,
                    DependencyProperty.UnsetValue,
                    DependencyProperty.UnsetValue
                };
            }
        }
        private sealed class LambdaConverter<TFrom, TTo> : IValueConverter
        {
            private readonly Func<TFrom?, TTo?> convert;
            private readonly Func<TTo?, TFrom?>? convertBack;
            private readonly bool convertFromNull;

            public LambdaConverter(
              Func<TFrom?, TTo?> convert,
              Func<TTo?, TFrom?>? convertBack = null,
              bool convertFromNull = true)
            {
                this.convert = convert;
                this.convertBack = convertBack;
                this.convertFromNull = convertFromNull;
            }

            public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
            {
                try
                {
                    if (value is TFrom from)
                        return this.convert(from);
                    return value == null && this.convertFromNull
                        ? this.convert(default(TFrom))
                        : value;
                }
                catch (Exception ex) when (!ex.IsCriticalException())
                {
                    throw new InvalidOperationException(string.Format("Callback threw an exception when converting from {0} to {1}.", typeof(TFrom), typeof(TTo)), ex);
                }
            }

            public object? ConvertBack(
              object? value,
              Type targetType,
              object parameter,
              CultureInfo culture)
            {
                try
                {
                    return this.convertBack != null && value is TTo to
                        ? this.convertBack(to)
                        : value;
                }
                catch (Exception ex) when (!ex.IsCriticalException())
                {
                    throw new InvalidOperationException(string.Format("Callback threw an exception when converting back to {0} from {1}.", typeof(TTo), typeof(TFrom)), ex);
                }
            }
        }

        private sealed class LambdaMultiConverter<TFrom1, TFrom2, TFrom3, TFrom4, TTo> :
      IMultiValueConverter
        {
            private readonly Func<TFrom1?, TFrom2?, TFrom3?, TFrom4?, TTo?> convert;
            private readonly Func<TTo, (TFrom1?, TFrom2?, TFrom3?, TFrom4?)>? convertBack;

            public LambdaMultiConverter(
              Func<TFrom1?, TFrom2?, TFrom3?, TFrom4?, TTo?> convert,
              Func<TTo?, (TFrom1?, TFrom2?, TFrom3?, TFrom4?)>? convertBack = null)
            {
                this.convert = convert;
                this.convertBack = convertBack;
            }

            public object? Convert(
              object[] values,
              Type targetType,
              object parameter,
              CultureInfo culture)
            {
                return values.Length == 4
                    && TryConvert(values[0], out TFrom1? t1)
                    && TryConvert(values[1], out TFrom2? t2)
                    && TryConvert(values[2], out TFrom3? t3)
                    && TryConvert(values[3], out TFrom4? t4)
                    ? this.convert(t1, t2, t3, t4)
                    : DependencyProperty.UnsetValue;

                static bool TryConvert<T>(object o, out T? t)
                {
                    if (o is T obj)
                    {
                        t = obj;
                        return true;
                    }
                    t = default(T);
                    return o == null;
                }
            }

            public object?[] ConvertBack(
              object value,
              Type[] targetTypes,
              object parameter,
              CultureInfo culture)
            {
                if (this.convertBack != null && value is TTo to)
                {
                    var valueTuple = this.convertBack(to);
                    return new object?[4]
                    {
                        valueTuple.Item1,
                        valueTuple.Item2,
                        valueTuple.Item3,
                        valueTuple.Item4
                    };
                }
                return new object[4]
                {
                    DependencyProperty.UnsetValue,
                    DependencyProperty.UnsetValue,
                    DependencyProperty.UnsetValue,
                    DependencyProperty.UnsetValue
                };
            }
        }
    }
}
