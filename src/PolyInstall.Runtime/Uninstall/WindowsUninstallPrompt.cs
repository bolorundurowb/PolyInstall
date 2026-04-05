using System.Runtime.InteropServices;

namespace PolyInstall.Runtime.Uninstall;

internal static class WindowsUninstallPrompt
{
    private const uint MbOkcancel = 0x00000001;
    private const uint MbIconwarning = 0x00000030;
    private const int Idcancel = 2;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    public static bool Confirm(string displayName)
    {
        var r = MessageBoxW(
            0,
            $"Remove {displayName} from this computer?",
            "Uninstall",
            MbOkcancel | MbIconwarning);
        return r != Idcancel;
    }
}
