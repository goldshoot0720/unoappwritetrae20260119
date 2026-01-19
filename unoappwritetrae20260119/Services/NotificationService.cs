using System;
using unoappwritetrae20260119.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;

namespace unoappwritetrae20260119.Services
{
    public class NotificationService
    {
        private Timer? _timer;
        private AppwriteService? _appwriteService;

        public void StartDailyScheduler(AppwriteService appwriteService)
        {
            _appwriteService = appwriteService;
            ScheduleNextCheck();
        }

        private void ScheduleNextCheck()
        {
            if (!OperatingSystem.IsWindows()) return;

            var now = DateTime.Now;
            // Target 6:00 AM
            var next6AM = now.Date.AddHours(6);
            
            // If it's already past 6 AM today, schedule for tomorrow 6 AM
            if (next6AM <= now)
            {
                next6AM = next6AM.AddDays(1);
            }

            var delay = next6AM - now;
            Debug.WriteLine($"Next notification check scheduled at: {next6AM} (in {delay.TotalHours:F2} hours)");

            // Dispose previous timer if any
            _timer?.Dispose();

            // Schedule single-shot timer
            _timer = new Timer(async _ => 
            {
                await CheckAndNotifyAsync();
                // Reschedule for the next day
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
            // Only run on Windows
            if (!OperatingSystem.IsWindows()) return;

            var today = DateTime.Now.Date;
            var threeDaysLater = today.AddDays(3);

            foreach (var sub in subscriptions)
            {
                if (DateTime.TryParse(sub.NextDate, out DateTime nextDate))
                {
                    // Check if date is within range [Today, Today+3]
                    if (nextDate.Date >= today && nextDate.Date <= threeDaysLater)
                    {
                        ShowNotification(sub, nextDate);
                    }
                }
            }
        }

        public void ShowTestNotification(Subscription sub)
        {
             if (DateTime.TryParse(sub.NextDate, out DateTime nextDate))
             {
                 ShowNotification(sub, nextDate);
             }
        }

        private void ShowNotification(Subscription sub, DateTime date)
        {
            var daysLeft = (date.Date - DateTime.Now.Date).Days;
            var msg = daysLeft == 0 ? "今天到期！" : $"還有 {daysLeft} 天到期";
            var title = $"訂閱即將到期: {sub.Name}";
            var text = $"{sub.Name} 將於 {date:yyyy/MM/dd} 扣款 ({msg})";

            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(text)
                    .Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show notification: {ex.Message}");
            }
        }
    }
}
