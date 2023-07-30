using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LinuxDebugger.ProjectSystem.ViewModels
{
    public abstract class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string? member = null)
        {
            if (member.IsMissing())
                return;
            PropertyChanged?.Invoke(this, new(member));
        }
        protected bool SetProperty<T>(ref T source,
                                      T value,
                                      IEqualityComparer<T>? comparer = null,
                                      [CallerMemberName] string? member = null)
        {
            comparer ??= EqualityComparer<T>.Default;
            var ret = false;
            if (!comparer.Equals(source, value))
            {
                source = value;
                PropertyChanged?.Invoke(this, new(member));
                ret = true;
            }
            return ret;
        }
    }
}
