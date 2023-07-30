using System.Runtime.InteropServices;

namespace LinuxDebugger.VisualStudio.Infrastructure
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
