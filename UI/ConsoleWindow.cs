using System;
using System.Runtime.InteropServices;

namespace RandomMagicConversion;

internal static class ConsoleWindow
{
    private const int SwHide = 0;

    public static void Hide()
    {
        IntPtr consoleHandle = GetConsoleWindow();
        if (consoleHandle != IntPtr.Zero)
            ShowWindow(consoleHandle, SwHide);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
