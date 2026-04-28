using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
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
    private enum AppSection
    {
        Subscriptions,
        OilMonitoring,
        BatteryStatus,
        UsDebt,
        FengTools,
        BankStats,
        FoodManagement,
        FengNotes,
        LotteryReason,
        FengCommon
    }

    private readonly AppwriteService _appwriteService;
    private readonly StartupService _startupService;
    private readonly NotificationService _notificationService;
    private readonly OqdMonitorService _oqdMonitorService;
    private DispatcherQueueTimer? _sleepWarningTimer;
    private const double CompactLayoutBreakpoint = 1180;
    private const double NarrowLayoutBreakpoint = 860;
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

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            _notificationService.StartDailyScheduler(_appwriteService);
        }

        if (OperatingSystem.IsWindows())
        {
            _oqdMonitorService.StartDailyScheduler(() =>
            {
                DispatcherQueue.TryEnqueue(() => _ = LoadOilMonitoringAsync(fetchLatest: false));
                return Task.CompletedTask;
            });
        }

        Loaded += MainPage_Loaded;
        SizeChanged += MainPage_SizeChanged;
        OilChartCanvas.SizeChanged += OilChartCanvas_SizeChanged;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout(ActualWidth);
        StartSleepWarningTimer();
        UpdateSleepWarning();

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
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

        SetActiveSection(AppSection.Subscriptions);
        await LoadSubscriptionsAsync();
        await LoadOilMonitoringAsync(fetchLatest: true);
    }

    private void StartSleepWarningTimer()
    {
        if (_sleepWarningTimer is not null)
        {
            return;
        }

        _sleepWarningTimer = DispatcherQueue.CreateTimer();
        _sleepWarningTimer.Interval = TimeSpan.FromMinutes(1);
        _sleepWarningTimer.Tick += (_, _) => UpdateSleepWarning();
        _sleepWarningTimer.Start();
    }

    private void UpdateSleepWarning()
    {
        var hour = DateTime.Now.Hour;

        if (hour is >= 0 and <= 2)
        {
            SleepWarningPanel.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 232, 163));
            SleepWarningPanel.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 229, 184, 64));
            SleepWarningText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 122, 82, 0));
            SleepWarningText.Text = "請入睡";
            SleepWarningPanel.Visibility = Visibility.Visible;
            return;
        }

        if (hour is >= 3 and <= 6)
        {
            SleepWarningPanel.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 218, 218));
            SleepWarningPanel.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 64, 64));
            SleepWarningText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 142, 21, 21));
            SleepWarningText.Text = "請入睡";
            SleepWarningPanel.Visibility = Visibility.Visible;
            return;
        }

        SleepWarningPanel.Visibility = Visibility.Collapsed;
    }

    private void SetActiveSection(AppSection section)
    {
        var isSubscriptions = section == AppSection.Subscriptions;
        var isOilMonitoring = section == AppSection.OilMonitoring;

        SubscriptionsSection.Visibility = isSubscriptions ? Visibility.Visible : Visibility.Collapsed;
        OilMonitoringSection.Visibility = isOilMonitoring ? Visibility.Visible : Visibility.Collapsed;
        ReferenceFeatureSection.Visibility = isSubscriptions || isOilMonitoring ? Visibility.Collapsed : Visibility.Visible;

        ResetNavItem(SubscriptionNavItem);
        ResetNavItem(OilMonitoringNavItem);
        ResetNavItem(BatteryStatusNavItem);
        ResetNavItem(UsDebtNavItem);
        ResetNavItem(FengToolsNavItem);
        ResetNavItem(BankStatsNavItem);
        ResetNavItem(FoodManagementNavItem);
        ResetNavItem(FengNotesNavItem);
        ResetNavItem(LotteryReasonNavItem);
        ResetNavItem(FengCommonNavItem);

        var activeNavItem = section switch
        {
            AppSection.Subscriptions => SubscriptionNavItem,
            AppSection.OilMonitoring => OilMonitoringNavItem,
            AppSection.BatteryStatus => BatteryStatusNavItem,
            AppSection.UsDebt => UsDebtNavItem,
            AppSection.FengTools => FengToolsNavItem,
            AppSection.BankStats => BankStatsNavItem,
            AppSection.FoodManagement => FoodManagementNavItem,
            AppSection.FengNotes => FengNotesNavItem,
            AppSection.LotteryReason => LotteryReasonNavItem,
            AppSection.FengCommon => FengCommonNavItem,
            _ => SubscriptionNavItem
        };

        SetActiveNavItem(activeNavItem);

        if (!isSubscriptions && !isOilMonitoring)
        {
            UpdateReferenceFeature(section);
        }
    }

    private void ResetNavItem(Border item)
    {
        item.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        item.BorderThickness = new Thickness(0);
    }

    private void SetActiveNavItem(Border item)
    {
        item.Background = (Brush)Application.Current.Resources["PanelSurfaceBrush"];
        item.BorderBrush = (Brush)Application.Current.Resources["PanelStrongBrush"];
        item.BorderThickness = new Thickness(1);
    }

    private void UpdateReferenceFeature(AppSection section)
    {
        var (eyebrow, title, description) = section switch
        {
            AppSection.BatteryStatus => ("Battery", "電池選單", "參考 Android 版的電池選單，用來顯示上次充滿電時間與剩餘電量估算。"),
            AppSection.UsDebt => ("US Debt", "US Debt", "參考 Android 版的美國國債入口，用來查看最新美國國債資料與更新時間。"),
            AppSection.FengTools => ("Fengbro Tools", "鋒兄工具", "參考 Android 版的工具入口，集中鋒兄比價與手機比價小工具。"),
            AppSection.BankStats => ("Banking", "銀行統計", "參考 Android 版的銀行統計，用來彙整銀行帳戶、存款與常用資料。"),
            AppSection.FoodManagement => ("Food", "食物管理", "參考 Android 版的食物管理，用來管理食物紀錄、價格與保存期限。"),
            AppSection.FengNotes => ("Notes", "常用筆記", "參考 Android 版的常用筆記，用來整理文章、連結與備註。"),
            AppSection.LotteryReason => ("Lottery", "最瞎結婚理由", "參考 Android 版的台彩逐期號碼與指定組合比對入口。"),
            AppSection.FengCommon => ("Common", "常用資料", "參考 Android 版的常用資料，用來查看常用帳號與共用資訊。"),
            _ => ("Reference", "功能入口", "此功能已依參考 Android 專案加入選單，後續可在 Uno 版接上完整資料與畫面。")
        };

        ReferenceFeatureEyebrowText.Text = eyebrow;
        ReferenceFeatureTitleText.Text = title;
        ReferenceFeatureDescriptionText.Text = description;
        ReferenceFeatureStatusText.Text = $"{title} 已加入 Uno 版主選單；目前保留入口與用途說明。";
    }

    private void OnSubscriptionsNavClick(object sender, RoutedEventArgs e) => SetActiveSection(AppSection.Subscriptions);

    private void OnOilMonitoringNavClick(object sender, RoutedEventArgs e) => SetActiveSection(AppSection.OilMonitoring);

    private void OnReferenceFeatureNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse<AppSection>(tag, out var section))
        {
            SetActiveSection(section);
        }
    }

    private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        var compact = width < CompactLayoutBreakpoint;
        var narrow = width < NarrowLayoutBreakpoint;

        MainShellGrid.Margin = compact ? new Thickness(16) : new Thickness(28);
        MainShellGrid.ColumnSpacing = compact ? 16 : 24;
        MainShellGrid.RowSpacing = compact ? 16 : 24;

        SidebarColumn.Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(260);
        ContentColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        MainShellBottomRow.Height = compact ? GridLength.Auto : new GridLength(0);

        Grid.SetColumn(SidebarPanel, 0);
        Grid.SetRow(SidebarPanel, 0);
        Grid.SetColumnSpan(SidebarPanel, compact ? 2 : 1);
        Grid.SetRowSpan(SidebarPanel, compact ? 1 : 2);

        Grid.SetColumn(ContentHostGrid, compact ? 0 : 1);
        Grid.SetRow(ContentHostGrid, compact ? 1 : 0);
        Grid.SetColumnSpan(ContentHostGrid, compact ? 2 : 1);
        Grid.SetRowSpan(ContentHostGrid, compact ? 1 : 2);

        SubscriptionsHeroActionsColumn.Width = compact ? new GridLength(0) : GridLength.Auto;
        SubscriptionsHeroBottomRow.Height = compact ? GridLength.Auto : new GridLength(0);
        Grid.SetColumn(SubscriptionsActionsCard, compact ? 0 : 1);
        Grid.SetRow(SubscriptionsActionsCard, compact ? 1 : 0);
        Grid.SetColumnSpan(SubscriptionsActionsCard, compact ? 2 : 1);

        OilHeroActionsColumn.Width = compact ? new GridLength(0) : GridLength.Auto;
        OilHeroBottomRow.Height = compact ? GridLength.Auto : new GridLength(0);
        Grid.SetColumn(OilHeroActionsPanel, compact ? 0 : 1);
        Grid.SetRow(OilHeroActionsPanel, compact ? 1 : 0);
        Grid.SetColumnSpan(OilHeroActionsPanel, compact ? 2 : 1);

        OilSummaryColumn.Width = compact ? new GridLength(0) : new GridLength(1.1, GridUnitType.Star);
        OilBodyBottomRow.Height = compact ? GridLength.Auto : new GridLength(0);
        Grid.SetColumn(OilSummaryCard, compact ? 0 : 1);
        Grid.SetRow(OilSummaryCard, compact ? 1 : 0);
        Grid.SetColumnSpan(OilSummaryCard, compact ? 2 : 1);

        if (narrow)
        {
            SubscriptionsMetaColumn2.Width = new GridLength(0);
            SubscriptionsMetaColumn3.Width = new GridLength(0);
            SubscriptionsMetaRow2.Height = GridLength.Auto;
            SubscriptionsMetaRow3.Height = GridLength.Auto;
            Grid.SetColumn(SubscriptionsMetaLayoutPanel, 0);
            Grid.SetColumn(SubscriptionsMetaThemePanel, 0);
            Grid.SetRow(SubscriptionsMetaLayoutPanel, 1);
            Grid.SetRow(SubscriptionsMetaThemePanel, 2);

            OilMetricsColumn2.Width = new GridLength(0);
            OilMetricsColumn3.Width = new GridLength(0);
            OilMetricsRow2.Height = GridLength.Auto;
            OilMetricsRow3.Height = GridLength.Auto;
            Grid.SetColumn(OilLastUpdatedCard, 0);
            Grid.SetColumn(OilHistoryCountCard, 0);
            Grid.SetRow(OilLastUpdatedCard, 1);
            Grid.SetRow(OilHistoryCountCard, 2);
        }
        else
        {
            SubscriptionsMetaColumn2.Width = GridLength.Auto;
            SubscriptionsMetaColumn3.Width = GridLength.Auto;
            SubscriptionsMetaRow2.Height = new GridLength(0);
            SubscriptionsMetaRow3.Height = new GridLength(0);
            Grid.SetColumn(SubscriptionsMetaLayoutPanel, 1);
            Grid.SetColumn(SubscriptionsMetaThemePanel, 2);
            Grid.SetRow(SubscriptionsMetaLayoutPanel, 0);
            Grid.SetRow(SubscriptionsMetaThemePanel, 0);

            OilMetricsColumn2.Width = new GridLength(1, GridUnitType.Star);
            OilMetricsColumn3.Width = new GridLength(1, GridUnitType.Star);
            OilMetricsRow2.Height = new GridLength(0);
            OilMetricsRow3.Height = new GridLength(0);
            Grid.SetColumn(OilLastUpdatedCard, 1);
            Grid.SetColumn(OilHistoryCountCard, 2);
            Grid.SetRow(OilLastUpdatedCard, 0);
            Grid.SetRow(OilHistoryCountCard, 0);
        }
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
            RestoreWindow();
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
        if ((OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) && sender is ToggleSwitch toggle)
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
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
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
