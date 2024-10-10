using System;
using System.Runtime.InteropServices;

namespace ClickerFixer.Desktop;

public class DwmApi
{
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
}
