using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace LinuxDebugger.ProjectSystem.PropertyPages.Editors
{
    internal sealed class ShellCommandLineControl : Control
    {
        private bool isUpdating;
        public string Value
        {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueProperty
            = DependencyProperty.Register(nameof(Value),
                                          typeof(string),
                                          typeof(ShellCommandLineControl),
                                          new()
                                          {
                                              PropertyChangedCallback = (o, e) => parse(o, (string?)e.NewValue)
                                          });




        public string CommandLine
        {
            get { return (string)GetValue(CommandLineProperty); }
            set { SetValue(CommandLineProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CommandLine.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommandLineProperty
            = DependencyProperty.Register(nameof(CommandLine),
                                          typeof(string),
                                          typeof(ShellCommandLineControl),
                                          new()
                                          {
                                              PropertyChangedCallback = (o, e) => updateValue(o)
                                          });

        public bool IgnoreExitCode
        {
            get { return (bool)GetValue(IgnoreExitCodeProperty); }
            set { SetValue(IgnoreExitCodeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IgnoreExitCode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IgnoreExitCodeProperty
            = DependencyProperty.Register(nameof(IgnoreExitCode),
                                          typeof(bool),
                                          typeof(ShellCommandLineControl),
                                          new()
                                          {
                                              PropertyChangedCallback = (o, e) => updateValue(o)
                                          });

        private static void parse(DependencyObject depObj, string? value)
        {
            var control = (ShellCommandLineControl)depObj;
            if (control.isUpdating)
            {
                return;
            }
            string? cmdLine;
            bool ignore;
            if (value.IsPresent())
            {
                (cmdLine, ignore) = LaunchProfileEnvironmentVariableEncoding
                    .ParseCommandLine(value);
            }
            else
            {
                cmdLine = null;
                ignore = false;
            }

            control.isUpdating = true;
            try
            {
                control.CommandLine = cmdLine!;
                control.IgnoreExitCode = ignore;
            }
            finally
            {
                control.isUpdating = false;
            }
        }
        private static void updateValue(DependencyObject depObj)
        {
            var control = (ShellCommandLineControl)depObj;
            if (control.isUpdating) return;
            var cmd = LaunchProfileEnvironmentVariableEncoding
                .FormatCommandLine(control.CommandLine,
                                   control.IgnoreExitCode);
            control.isUpdating = true;
            try
            {
                control.SetCurrentValue(ValueProperty, cmd);
            }
            finally
            {
                control.isUpdating = false;
            }
        }

    }
}
