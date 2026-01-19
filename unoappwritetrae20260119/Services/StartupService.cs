using Microsoft.Win32;
using System;
using System.Runtime.Versioning;

namespace unoappwritetrae20260119.Services
{
    public class StartupService
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "UnoAppwriteTrae";

        [SupportedOSPlatform("windows")]
        public void SetStartup(bool enable)
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return;

                if (enable)
                {
                    // Get the path to the executable
                    var location = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(location))
                    {
                        key.SetValue(AppName, $"\"{location}\" --autostart");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set startup: {ex.Message}");
            }
        }

        [SupportedOSPlatform("windows")]
        public bool IsStartupEnabled()
        {
            if (!OperatingSystem.IsWindows()) return false;

             try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
             catch
             {
                 return false;
             }
        }
    }
}
