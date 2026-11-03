using System.Runtime.InteropServices;

namespace CircleLight.Services;

internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyIcon(IntPtr hIcon);
}
