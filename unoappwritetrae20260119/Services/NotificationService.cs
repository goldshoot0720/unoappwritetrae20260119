using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using unoappwritetrae20260119.Models;

namespace unoappwritetrae20260119.Services
{
    public class NotificationService
    {
        private Timer? _timer;
        private AppwriteService? _appwriteService;
        private bool _schedulerStarted;

        public event Action<List<string>>? ExpiringNotificationsChanged;

        public void StartDailyScheduler(AppwriteService appwriteService)
        {
            _appwriteService = appwriteService;

            if (_schedulerStarted)
            {
                return;
            }

            _schedulerStarted = true;
            ScheduleNextCheck();
        }

        private void ScheduleNextCheck()
        {
            if (!OperatingSystem.IsWindows()) return;

            var now = DateTime.Now;
            var next6AM = now.Date.AddHours(6);

            if (next6AM <= now)
            {
                next6AM = next6AM.AddDays(1);
            }

            var delay = next6AM - now;
            Debug.WriteLine($"Next notification check scheduled at: {next6AM} (in {delay.TotalHours:F2} hours)");

            _timer?.Dispose();
            _timer = new Timer(async _ =>
            {
                await CheckAndNotifyAsync();
                ScheduleNextCheck();
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        private async Task CheckAndNotifyAsync()
        {
            if (_appwriteService == null) return;

            try
            {
                var subscriptions = await _appwriteService.GetSubscriptionsAsync();
                CheckAndNotify(subscriptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during scheduled check: {ex.Message}");
            }
        }

        public void CheckAndNotify(List<Subscription> subscriptions)
        {
            if (!OperatingSystem.IsWindows()) return;

            var today = DateTime.Now.Date;
            var threeDaysLater = today.AddDays(3);
            var messages = new List<string>();

            foreach (var sub in subscriptions)
            {
                if (DateTime.TryParse(sub.NextDate, out var nextDate) &&
                    nextDate.Date >= today &&
                    nextDate.Date <= threeDaysLater)
                {
                    ShowNotification(sub, nextDate);

                    var daysText = GetDaysText(nextDate);
                    var accountPart = string.IsNullOrWhiteSpace(sub.Account) ? "" : $"{sub.Account} - ";
                    messages.Add($"{accountPart}{sub.Name} {daysText} ({nextDate:yyyy-MM-dd})");
                }
            }

            ExpiringNotificationsChanged?.Invoke(messages);
        }

        public void ShowTestNotification(Subscription sub)
        {
            if (!DateTime.TryParse(sub.NextDate, out var nextDate))
            {
                return;
            }

            ShowNotification(sub, nextDate);
            ExpiringNotificationsChanged?.Invoke(new List<string>
            {
                $"{sub.Name} {GetDaysText(nextDate)} ({nextDate:yyyy-MM-dd})"
            });
        }

        private void ShowNotification(Subscription sub, DateTime date)
        {
            var title = $"訂閱提醒: {sub.Name}";
            var text = $"{sub.Name} 將於 {date:yyyy/MM/dd} 到期 ({GetDaysText(date)})";

            try
            {
                var helperPath = ResolveHelperPath();
                if (!string.IsNullOrWhiteSpace(helperPath) && File.Exists(helperPath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = helperPath,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    startInfo.ArgumentList.Add(title);
                    startInfo.ArgumentList.Add(text);
                    Process.Start(startInfo);
                    return;
                }

                Debug.WriteLine($"Notification helper not found. {title} - {text}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show notification: {ex.Message}");
            }
        }

        private static string GetDaysText(DateTime date)
        {
            var daysLeft = (date.Date - DateTime.Now.Date).Days;
            return daysLeft == 0 ? "今天到期" :
                   daysLeft == 1 ? "明天到期" :
                   $"{daysLeft} 天後到期";
        }

        private static string? ResolveHelperPath()
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                return null;
            }

            var appDirectory = Path.GetDirectoryName(processPath);
            var workspaceRoot = Path.GetFullPath(Path.Combine(appDirectory!, "..", "..", ".."));
            return Path.Combine(
                workspaceRoot,
                "UnoAppwriteNotificationHelper",
                "bin",
                "Debug",
                "net9.0-windows10.0.19041.0",
                "UnoAppwriteNotificationHelper.exe");
        }
    }
}
