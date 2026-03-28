using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ArduinoBridge;

public static class AutoStart
{
    private const string AppName = "ArduinoBridge";

    public static bool IsEnabled()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return IsEnabledWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return File.Exists(GetMacPlistPath());
        return false;
    }

    public static void SetEnabled(bool enabled)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            SetEnabledWindows(enabled);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            SetEnabledMac(enabled);
    }

    private static string GetExePath()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            exe = Path.Combine(AppContext.BaseDirectory, AppName + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : ""));
        return exe;
    }

    [SupportedOSPlatform("windows")]
    private static bool IsEnabledWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
        return key?.GetValue(AppName) is string val && val.Length > 0;
    }

    [SupportedOSPlatform("windows")]
    private static void SetEnabledWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key is null) return;
        if (enabled)
            key.SetValue(AppName, $"\"{GetExePath()}\"");
        else
            key.DeleteValue(AppName, false);
    }

    private static string GetMacPlistPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents", "com.arduinobridge.plist");

    private static void SetEnabledMac(bool enabled)
    {
        var plistPath = GetMacPlistPath();
        if (enabled)
        {
            var plist = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>com.arduinobridge</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{GetExePath()}</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>KeepAlive</key>
                    <false/>
                </dict>
                </plist>
                """;
            Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
            File.WriteAllText(plistPath, plist);
        }
        else if (File.Exists(plistPath))
        {
            File.Delete(plistPath);
        }
    }
}
