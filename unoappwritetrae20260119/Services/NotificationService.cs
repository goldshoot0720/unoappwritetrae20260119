using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security;
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
        private bool _notificationsRegistered;
        private object? _notificationManager;
        private Type? _appNotificationType;

        public event Action<List<string>>? ExpiringNotificationsChanged;

        public void StartDailyScheduler(AppwriteService appwriteService)
        {
            _appwriteService = appwriteService;
            TryEnsureWindowsNotificationsRegistered();

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
                if (TryEnsureWindowsNotificationsRegistered())
                {
                    var payload = $"""
                        <toast>
                          <visual>
                            <binding template="ToastGeneric">
                              <text>{SecurityElement.Escape(title)}</text>
                              <text>{SecurityElement.Escape(text)}</text>
                            </binding>
                          </visual>
                        </toast>
                        """;

                    var notification = Activator.CreateInstance(_appNotificationType!, payload);
                    var showMethod = _notificationManager!.GetType()
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .FirstOrDefault(method => method.Name == "Show" && method.GetParameters().Length == 1);

                    showMethod?.Invoke(_notificationManager, new[] { notification });
                    if (showMethod is not null)
                    {
                        return;
                    }
                }

                Debug.WriteLine($"{title} - {text}");
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

        private bool TryEnsureWindowsNotificationsRegistered()
        {
            if (_notificationsRegistered)
            {
                return true;
            }

            var managerType = Type.GetType("Microsoft.Windows.AppNotifications.AppNotificationManager, Microsoft.Windows.AppNotifications.Projection");
            _appNotificationType = Type.GetType("Microsoft.Windows.AppNotifications.AppNotification, Microsoft.Windows.AppNotifications.Projection");
            if (managerType is null || _appNotificationType is null)
            {
                return false;
            }

            var isSupportedMethod = managerType.GetMethod("IsSupported", BindingFlags.Public | BindingFlags.Static);
            if (isSupportedMethod?.Invoke(null, null) is false)
            {
                return false;
            }

            _notificationManager = managerType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (_notificationManager is null)
            {
                return false;
            }

            _notificationManager.GetType().GetMethod("Register", Type.EmptyTypes)?.Invoke(_notificationManager, null);
            _notificationsRegistered = true;
            return true;
        }
    }
}
