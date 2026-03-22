using System;
using Microsoft.Extensions.Logging;
using Uno.Resizetizer;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace unoappwritetrae20260119;

public partial class App : Application
{
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_Bool(IntPtr receiver, IntPtr selector, bool arg1);

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();
#if DEBUG
        MainWindow.UseStudio();
#endif


        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;

            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        if (rootFrame.Content == null)
        {
            // When the navigation stack isn't restored navigate to the first page,
            // configuring the new page by passing required information as a navigation
            // parameter
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }

        MainWindow.SetWindowIcon();
        // Ensure the current window is active
        MainWindow.Activate();
        
        // Handle autostart argument
        var commandLineArgs = Environment.GetCommandLineArgs();
        if (commandLineArgs.Contains("--autostart"))
        {
            _ = HideWindowOnAutostartAsync();
        }
    }

    private async Task HideWindowOnAutostartAsync()
    {
        // Give the desktop host a brief moment to finish creating the native window
        // before we send it to the background.
        await Task.Delay(300);
        HideWindow();
    }
    
    public void HideWindow()
    {
        MainWindow?.DispatcherQueue.TryEnqueue(() => 
        {
            try
            {
#if WINDOWS
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                
                appWindow.Hide();
#elif __MACOS__
                HideMacApplication();
#else
                MainWindow?.Close();
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to hide: {ex.Message}");
            }
        });
    }

    public void ShowWindow()
    {
        MainWindow?.DispatcherQueue.TryEnqueue(() => 
        {
            try
            {
#if WINDOWS
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                
                appWindow.Show();
                
                // Also restore if minimized
                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    if (presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
                    {
                        presenter.Restore();
                    }
                }
                
                // Bring to front
                MainWindow.Activate();
#elif __MACOS__
                ShowMacApplication();
                MainWindow?.Activate();
#else
                MainWindow?.Activate();
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show: {ex.Message}");
            }
        });
    }

    private static void HideMacApplication()
    {
        try
        {
            var app = GetSharedMacApplication();
            if (app == IntPtr.Zero)
            {
                return;
            }

            var hideSelector = sel_registerName("hide:");
            objc_msgSend_Void_IntPtr(app, hideSelector, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to hide macOS app: {ex.Message}");
        }
    }

    private static void ShowMacApplication()
    {
        try
        {
            var app = GetSharedMacApplication();
            if (app == IntPtr.Zero)
            {
                return;
            }

            var unhideSelector = sel_registerName("unhide:");
            var activateSelector = sel_registerName("activateIgnoringOtherApps:");
            objc_msgSend_Void_IntPtr(app, unhideSelector, IntPtr.Zero);
            objc_msgSend_Void_Bool(app, activateSelector, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show macOS app: {ex.Message}");
        }
    }

    private static IntPtr GetSharedMacApplication()
    {
        var nsApplicationClass = objc_getClass("NSApplication");
        if (nsApplicationClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var sharedApplicationSelector = sel_registerName("sharedApplication");
        return objc_msgSend_IntPtr(nsApplicationClass, sharedApplicationSelector);
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails
    /// </summary>
    /// <param name="sender">The Frame which failed navigation</param>
    /// <param name="e">Details about the navigation failure</param>
    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    /// <summary>
    /// Configures global Uno Platform logging
    /// </summary>
    public static void InitializeLogging()
    {
#if DEBUG
        // Logging is disabled by default for release builds, as it incurs a significant
        // initialization cost from Microsoft.Extensions.Logging setup. If startup performance
        // is a concern for your application, keep this disabled. If you're running on the web or
        // desktop targets, you can use URL or command line parameters to enable it.
        //
        // For more performance documentation: https://platform.uno/docs/articles/Uno-UI-Performance.html

        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());

            // Log to the Visual Studio Debug console
            builder.AddConsole();
#else
            builder.AddConsole();
#endif

            // Exclude logs below this level
            builder.SetMinimumLevel(LogLevel.Information);

            // Default filters for Uno Platform namespaces
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);

            // Generic Xaml events
            // builder.AddFilter("Microsoft.UI.Xaml", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.VisualStateGroup", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.StateTriggerBase", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.UIElement", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.FrameworkElement", LogLevel.Trace );

            // Layouter specific messages
            // builder.AddFilter("Microsoft.UI.Xaml.Controls", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Layouter", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Panel", LogLevel.Debug );

            // builder.AddFilter("Windows.Storage", LogLevel.Debug );

            // Binding related messages
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );

            // Binder memory references tracking
            // builder.AddFilter("Uno.UI.DataBinding.BinderReferenceHolder", LogLevel.Debug );

            // DevServer and HotReload related
            // builder.AddFilter("Uno.UI.RemoteControl", LogLevel.Information);

            // Debug JS interop
            // builder.AddFilter("Uno.Foundation.WebAssemblyRuntime", LogLevel.Debug );
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
