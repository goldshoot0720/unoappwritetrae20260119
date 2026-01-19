using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using unoappwritetrae20260119.Models;
using unoappwritetrae20260119.Services;

namespace unoappwritetrae20260119;

public sealed partial class MainPage : Page
{
    private AppwriteService _appwriteService;
    private StartupService _startupService;
    private NotificationService _notificationService;

    public ObservableCollection<Subscription> Subscriptions { get; } = new ObservableCollection<Subscription>();

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(MainPage), new PropertyMetadata(false));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public MainPage()
    {
        this.InitializeComponent();
        _appwriteService = new AppwriteService();
        _startupService = new StartupService();
        _notificationService = new NotificationService();
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (System.OperatingSystem.IsWindows())
        {
            // Initialize switch state
            bool isStartup = _startupService.IsStartupEnabled();
            StartupSwitch.IsOn = isStartup;

            // User requested "Set start on boot", so enable it if not already enabled
            // Also update the key to ensure it has the latest arguments (e.g. --autostart)
            if (!isStartup)
            {
                 _startupService.SetStartup(true);
                 StartupSwitch.IsOn = true;
            }
            else
            {
                // Force update to ensure --autostart argument is present
                _startupService.SetStartup(true);
            }
        }
        else
        {
            StartupSwitch.IsEnabled = false;
        }

        await LoadSubscriptionsAsync();
    }

    private void StartupSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (System.OperatingSystem.IsWindows() && sender is ToggleSwitch toggle)
        {
            _startupService.SetStartup(toggle.IsOn);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadSubscriptionsAsync();
    }

    private void TestNotification_Click(object sender, RoutedEventArgs e)
    {
        if (System.OperatingSystem.IsWindows())
        {
            var dummySub = new Subscription 
            { 
                Name = "測試訂閱", 
                NextDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"), // Tomorrow
                Price = 100
            };
            _notificationService.ShowTestNotification(dummySub);
        }
    }

    private async System.Threading.Tasks.Task LoadSubscriptionsAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        Subscriptions.Clear();

        try
        {
            var items = await _appwriteService.GetSubscriptionsAsync();
            
            // Sort items by NextDate ascending (nearest first)
            var sortedItems = items.OrderBy(x => 
            {
                if (DateTime.TryParse(x.NextDate, out DateTime date))
                {
                    return date;
                }
                return DateTime.MaxValue; // Put invalid/null dates at the end
            });

            foreach (var item in sortedItems)
            {
                Subscriptions.Add(item);
            }
            
            // Check for expiring subscriptions
            _notificationService.CheckAndNotify(items);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
