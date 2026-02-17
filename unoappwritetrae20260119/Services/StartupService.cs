using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace unoappwritetrae20260119.Services
{
    public class StartupService
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "UnoAppwriteTrae";

        // 需要移除的其他競爭 auto-start 項目
        private static readonly string[] CompetingRegistryEntries =
        {
            "AvaloniaAppwriteSubscriptionManager",
            "AvaloniaAppwriteApp",
            "AppwriteSubscriptionViewer"
        };

        // 需要終止的其他競爭進程名稱 (不含 .exe)
        private static readonly string[] CompetingProcessNames =
        {
            "avaloniaappwritetrae20260119",
            "appwritewpftrae20260118"
        };

        [SupportedOSPlatform("windows")]
        public void SetStartup(bool enable)
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                if (enable)
                {
                    RemoveCompetingEntries();
                    KillCompetingProcesses();
                }

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

        [SupportedOSPlatform("windows")]
        private void RemoveCompetingEntries()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return;

                foreach (var name in CompetingRegistryEntries)
                {
                    try
                    {
                        if (key.GetValue(name) != null)
                        {
                            key.DeleteValue(name, throwOnMissingValue: false);
                            System.Diagnostics.Debug.WriteLine($"Removed competing registry entry: {name}");
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        [SupportedOSPlatform("windows")]
        private void KillCompetingProcesses()
        {
            try
            {
                foreach (var processName in CompetingProcessNames)
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(processName);
                        foreach (var proc in processes)
                        {
                            try
                            {
                                proc.Kill();
                                System.Diagnostics.Debug.WriteLine($"Killed competing process: {processName} (PID {proc.Id})");
                            }
                            catch
                            {
                            }
                            finally
                            {
                                proc.Dispose();
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Kills all other instances of the current application (same process name, different PID).
        /// Call this early in Program.Main to ensure single-instance behavior.
        /// </summary>
        public static void KillOtherInstances()
        {
            try
            {
                var currentPid = Environment.ProcessId;
                var currentProcessName = Process.GetCurrentProcess().ProcessName;
                
                var processes = Process.GetProcessesByName(currentProcessName);
                foreach (var proc in processes)
                {
                    try
                    {
                        if (proc.Id != currentPid)
                        {
                            proc.Kill();
                            System.Diagnostics.Debug.WriteLine($"Killed other instance: {currentProcessName} (PID {proc.Id})");
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KillOtherInstances error: {ex.Message}");
            }
        }
    }
}
