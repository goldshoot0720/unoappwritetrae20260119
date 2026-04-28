using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

if (!OperatingSystem.IsWindows() || !AppNotificationManager.IsSupported())
{
    return;
}

var title = args.Length > 0 ? args[0] : "訂閱提醒";
var text = args.Length > 1 ? args[1] : "你有新的提醒";

AppNotificationManager.Default.Register();

var notification = new AppNotificationBuilder()
    .AddText(title)
    .AddText(text)
    .BuildNotification();

AppNotificationManager.Default.Show(notification);

await Task.Delay(1500);
