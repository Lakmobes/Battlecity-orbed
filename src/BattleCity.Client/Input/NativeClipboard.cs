using System.Runtime.InteropServices;

namespace BattleCity.Client.Input;

/// <summary>Windows clipboard read for login/server paste (MonoGame has no clipboard API).</summary>
internal static class NativeClipboard
{
    public static bool TryGetText(out string text)
    {
        text = string.Empty;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (!OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                text = Marshal.PtrToStringUni(pointer) ?? string.Empty;
                text = text.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
                return text.Length > 0;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private const uint CfUnicodeText = 13;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}
