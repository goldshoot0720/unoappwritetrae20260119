using System;
using unoappwritetrae20260119.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace unoappwritetrae20260119.Services
{
    public class NotificationService
    {
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
                // Use PowerShell to show notification since we don't have direct WinRT access in net9.0-desktop (Uno default) easily
                string script = $@"
$xml = @""
<toast>
  <visual>
    <binding template='ToastGeneric'>
      <text>{title}</text>
      <text>{text}</text>
    </binding>
  </visual>
</toast>
""@
$xmlDocument = [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType=WindowsRuntime]::new()
$xmlDocument.LoadXml($xml)
$toast = [Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType=WindowsRuntime]::new($xmlDocument)
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime]::CreateToastNotifier('UnoAppwriteTrae').Show($toast)
";
                
                var psCommand = System.Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {psCommand}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show notification: {ex.Message}");
            }
        }
    }
}
