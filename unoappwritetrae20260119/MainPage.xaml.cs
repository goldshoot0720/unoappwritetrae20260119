using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
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
    private bool _sortByName = true;

    public ObservableCollection<Subscription> Subscriptions { get; } = new ObservableCollection<Subscription>();

    public ICommand RestoreCommand => new RelayCommand(RestoreWindow);

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
        
        // Initialize Tray Icon
        var trayIcon = new H.NotifyIcon.TaskbarIcon
        {
            ToolTipText = "UnoAppwriteTrae",
            IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///icon.ico")),
            LeftClickCommand = RestoreCommand
        };
        RootGrid.Children.Add(trayIcon);

        _appwriteService = new AppwriteService();
        _startupService = new StartupService();
        _notificationService = new NotificationService();

        // Subscribe to in-window notification events
        _notificationService.ExpiringNotificationsChanged += OnExpiringNotificationsChanged;
        
        // Start daily scheduler for notifications (6:00 AM)
        if (System.OperatingSystem.IsWindows())
        {
             _notificationService.StartDailyScheduler(_appwriteService);
        }

        Loaded += MainPage_Loaded;
    }

    private void OnExpiringNotificationsChanged(List<string> messages)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (messages == null || messages.Count == 0)
            {
                NotificationPanel.Visibility = Visibility.Collapsed;
                return;
            }

            NotificationList.ItemsSource = messages;
            NotificationPanel.Visibility = Visibility.Visible;
        });
    }

    private void DismissNotification_Click(object sender, RoutedEventArgs e)
    {
        NotificationPanel.Visibility = Visibility.Collapsed;
    }

    private void RestoreWindow()
    {
        if (Application.Current is App app)
        {
            app.ShowWindow();
        }
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

    private async void SortByNameButton_Click(object sender, RoutedEventArgs e)
    {
        _sortByName = !_sortByName;
        SortByNameButton.Content = _sortByName ? "按日期排序" : "按名稱排序";
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
            
            // Sort items based on current sort mode
            IEnumerable<Subscription> sortedItems;
            if (_sortByName)
            {
                sortedItems = items.OrderBy(x => x.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase);
            }
            else
            {
                sortedItems = items.OrderBy(x => 
                {
                    if (DateTime.TryParse(x.NextDate, out DateTime date))
                    {
                        return date;
                    }
                    return DateTime.MaxValue;
                });
            }

            foreach (var item in sortedItems)
            {
                Subscriptions.Add(item);
            }
            
            // Check for expiring subscriptions (both toast + in-window)
            _notificationService.CheckAndNotify(items);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
