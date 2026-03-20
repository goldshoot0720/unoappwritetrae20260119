using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using unoappwritetrae20260119.Models;
using unoappwritetrae20260119.Services;

namespace unoappwritetrae20260119;

public sealed partial class MainPage : Page
{
    private readonly AppwriteService _appwriteService;
    private readonly StartupService _startupService;
    private readonly NotificationService _notificationService;
    private readonly OqdMonitorService _oqdMonitorService;
    private bool _sortByName = true;

    public ObservableCollection<Subscription> Subscriptions { get; } = new();
    public ObservableCollection<OqdPricePoint> OqdHistory { get; } = new();

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
        InitializeComponent();

#if WINDOWS
        var trayIcon = new H.NotifyIcon.TaskbarIcon
        {
            ToolTipText = "UnoAppwriteTrae",
            IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///icon.ico")),
            LeftClickCommand = RestoreCommand
        };
        RootGrid.Children.Add(trayIcon);
#endif

        _appwriteService = new AppwriteService();
        _startupService = new StartupService();
        _notificationService = new NotificationService();
        _oqdMonitorService = new OqdMonitorService();

        _notificationService.ExpiringNotificationsChanged += OnExpiringNotificationsChanged;

        if (OperatingSystem.IsWindows())
        {
            _notificationService.StartDailyScheduler(_appwriteService);
            _oqdMonitorService.StartDailyScheduler(() =>
            {
                DispatcherQueue.TryEnqueue(() => _ = LoadOilMonitoringAsync(fetchLatest: false));
                return Task.CompletedTask;
            });
        }

        Loaded += MainPage_Loaded;
        OilChartCanvas.SizeChanged += OilChartCanvas_SizeChanged;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsWindows())
        {
            var isStartup = _startupService.IsStartupEnabled();
            StartupSwitch.IsOn = isStartup;

            if (!isStartup)
            {
                _startupService.SetStartup(true);
                StartupSwitch.IsOn = true;
            }
            else
            {
                _startupService.SetStartup(true);
            }
        }
        else
        {
            StartupSwitch.IsEnabled = false;
        }

        SetActiveSection(isOilMonitoring: false);
        await LoadSubscriptionsAsync();
        await LoadOilMonitoringAsync(fetchLatest: true);
    }

    private void SetActiveSection(bool isOilMonitoring)
    {
        SubscriptionsSection.Visibility = isOilMonitoring ? Visibility.Collapsed : Visibility.Visible;
        OilMonitoringSection.Visibility = isOilMonitoring ? Visibility.Visible : Visibility.Collapsed;

        SubscriptionNavItem.Background = isOilMonitoring
            ? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : (Brush)Application.Current.Resources["PanelSurfaceBrush"];
        SubscriptionNavItem.BorderThickness = isOilMonitoring ? new Thickness(0) : new Thickness(1);

        OilMonitoringNavItem.Background = isOilMonitoring
            ? (Brush)Application.Current.Resources["AccentPanelBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        OilMonitoringNavItem.BorderThickness = isOilMonitoring ? new Thickness(1) : new Thickness(0);
    }

    private void OnSubscriptionsNavClick(object sender, RoutedEventArgs e) => SetActiveSection(isOilMonitoring: false);

    private void OnOilMonitoringNavClick(object sender, RoutedEventArgs e) => SetActiveSection(isOilMonitoring: true);

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

    private void StartupSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsWindows() && sender is ToggleSwitch toggle)
        {
            _startupService.SetStartup(toggle.IsOn);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadSubscriptionsAsync();

    private async void SortByNameButton_Click(object sender, RoutedEventArgs e)
    {
        _sortByName = !_sortByName;
        SortByNameButton.Content = _sortByName ? "按日期排序" : "按名稱排序";
        await LoadSubscriptionsAsync();
    }

    private async void RefreshOilMonitoringButton_Click(object sender, RoutedEventArgs e) => await LoadOilMonitoringAsync(fetchLatest: false);

    private async void FetchOilMonitoringButton_Click(object sender, RoutedEventArgs e) => await LoadOilMonitoringAsync(fetchLatest: true);

    private void TestNotification_Click(object sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsWindows())
        {
            var dummySub = new Subscription
            {
                Name = "測試通知",
                NextDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"),
                Price = 100
            };
            _notificationService.ShowTestNotification(dummySub);
        }
    }

    private async Task LoadSubscriptionsAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        Subscriptions.Clear();

        try
        {
            var items = await _appwriteService.GetSubscriptionsAsync();

            var sortedItems = _sortByName
                ? items.OrderBy(x => x.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                : items.OrderBy(x =>
                {
                    if (DateTime.TryParse(x.NextDate, out var date))
                    {
                        return date;
                    }

                    return DateTime.MaxValue;
                });

            foreach (var item in sortedItems)
            {
                Subscriptions.Add(item);
            }

            SubscriptionCountText.Text = $"共 {Subscriptions.Count} 筆";
            _notificationService.CheckAndNotify(items);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadSubscriptionsAsync ERROR: {ex}");
            SubscriptionCountText.Text = "共 0 筆";
            NotificationList.ItemsSource = new List<string> { $"載入失敗: {ex.Message}" };
            NotificationPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadOilMonitoringAsync(bool fetchLatest)
    {
        OilMonitoringStatusText.Text = fetchLatest ? "抓取中..." : "整理資料中...";

        try
        {
            if (fetchLatest)
            {
                await _oqdMonitorService.FetchAndPersistLatestAsync();
            }
            else
            {
                await _oqdMonitorService.EnsureTodaySnapshotAsync();
            }

            var history = await _oqdMonitorService.LoadHistoryAsync();
            OqdHistory.Clear();
            foreach (var item in history)
            {
                OqdHistory.Add(item);
            }

            var latest = OqdHistory.LastOrDefault();
            OilLatestPriceText.Text = latest is null ? "--" : latest.Price.ToString("0.00");
            OilLastUpdatedText.Text = latest is null ? "尚未抓取" : latest.RecordedAt.ToString("yyyy-MM-dd HH:mm");
            OilHistoryCountText.Text = $"{OqdHistory.Count} 天";
            OilSourceText.Text = "來源: gulfmerc.com 首頁 OQD Daily Marker Price";
            OilMonitoringStatusText.Text = "每日 13:00 自動抓取";

            RenderOilChart();
        }
        catch (Exception ex)
        {
            OilMonitoringStatusText.Text = $"抓取失敗: {ex.Message}";
            OilSourceText.Text = "請確認網路與 gulfmerc.com 可連線";
            RenderOilChart();
        }
    }

    private void OilChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderOilChart();
    }

    private void RenderOilChart()
    {
        OilChartCanvas.Children.Clear();

        if (OqdHistory.Count == 0 || OilChartCanvas.ActualWidth <= 0 || OilChartCanvas.ActualHeight <= 0)
        {
            ChartEmptyState.Visibility = Visibility.Visible;
            return;
        }

        ChartEmptyState.Visibility = Visibility.Collapsed;

        var width = OilChartCanvas.ActualWidth;
        var height = OilChartCanvas.ActualHeight;
        const double paddingLeft = 10;
        const double paddingTop = 12;
        const double paddingBottom = 18;
        const double paddingRight = 10;

        var points = OqdHistory.ToList();
        var minPrice = (double)points.Min(x => x.Price);
        var maxPrice = (double)points.Max(x => x.Price);
        var range = Math.Max(maxPrice - minPrice, 1);
        var chartWidth = Math.Max(width - paddingLeft - paddingRight, 1);
        var chartHeight = Math.Max(height - paddingTop - paddingBottom, 1);

        var polyline = new Polyline
        {
            Stroke = (Brush)Application.Current.Resources["AccentBrush"],
            StrokeThickness = 3
        };

        for (var i = 0; i < points.Count; i++)
        {
            var x = paddingLeft + (chartWidth * i / Math.Max(points.Count - 1, 1));
            var normalized = ((double)points[i].Price - minPrice) / range;
            var y = paddingTop + chartHeight - (chartHeight * normalized);

            polyline.Points.Add(new Point(x, y));

            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = (Brush)Application.Current.Resources["AccentStrongBrush"]
            };
            Canvas.SetLeft(dot, x - 4);
            Canvas.SetTop(dot, y - 4);
            OilChartCanvas.Children.Add(dot);
        }

        OilChartCanvas.Children.Add(polyline);

        var firstLabel = new TextBlock
        {
            Text = points.First().RecordedAt.ToString("MM-dd"),
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            FontSize = 12
        };
        Canvas.SetLeft(firstLabel, paddingLeft);
        Canvas.SetTop(firstLabel, height - 18);
        OilChartCanvas.Children.Add(firstLabel);

        var lastLabel = new TextBlock
        {
            Text = points.Last().RecordedAt.ToString("MM-dd"),
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            FontSize = 12
        };
        Canvas.SetLeft(lastLabel, Math.Max(width - 48, paddingLeft));
        Canvas.SetTop(lastLabel, height - 18);
        OilChartCanvas.Children.Add(lastLabel);
    }
}
