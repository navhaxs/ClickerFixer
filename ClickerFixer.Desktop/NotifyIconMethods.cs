using System;
using System.Runtime.InteropServices;

namespace ClickerFixer.Desktop;

public class NotifyIconMethods
{
    public static IntPtr GetNotifyIconOverflowWindowHandle()
    {
        IntPtr hWndTray = NativeMethods.FindWindow("NotifyIconOverflowWindow", null);
        if (hWndTray != IntPtr.Zero)
        {
            IntPtr hWndToolbar = NativeMethods.FindWindowEx(hWndTray, IntPtr.Zero, "ToolbarWindow32", null);
            return hWndToolbar;
        }

        return IntPtr.Zero;
    }

    public static IntPtr FindIconHandleForCurrentProcess(IntPtr notifyIconHandle)
    {
        int currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

        try
        {
            IntPtr windowHandle = notifyIconHandle; /* obtain your window handle here */
            ;
            string className = NativeMethods.GetWindowClassName(windowHandle);
            Console.WriteLine($"The class name of the window is: {className}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.WriteLine($"Error getting class name: {ex.Message}");
        }

        int buttonCount =
            (int)NativeMethods.SendMessage(notifyIconHandle, NativeMethods.TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);

        for (int i = 0; i < buttonCount; i++)
        {
            NativeMethods.TBBUTTON tbButton = new NativeMethods.TBBUTTON();
            NativeMethods.SendMessage(notifyIconHandle, NativeMethods.TB_GETBUTTON, (IntPtr)i, ref tbButton);

            IntPtr iconHandle = tbButton.dwData;
            int iconProcessId;
            NativeMethods.GetWindowThreadProcessId(iconHandle, out iconProcessId);

            if (iconProcessId == currentProcessId)
            {
                return iconHandle;
            }
        }

        return IntPtr.Zero;
    }

    public static NativeMethods.NOTIFYICONIDENTIFIER GetNotifyIconIdentifier(IntPtr iconHandle)
    {
        NativeMethods.NOTIFYICONIDENTIFIER identifier = new NativeMethods.NOTIFYICONIDENTIFIER
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONIDENTIFIER)),
            hWnd = iconHandle,
            uID = 0
        };

        NativeMethods.RECT iconLocation;
        int result = NativeMethods.Shell_NotifyIconGetRect(ref identifier, out iconLocation);

        if (result == 0) // S_OK
        {
            return identifier;
        }
        else
        {
            throw new Exception($"Failed to get NOTIFYICONIDENTIFIER. Error code: {result}");
        }
    }
}