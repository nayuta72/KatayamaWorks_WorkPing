using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using WorkPing.Features.AccountSettings.ViewModels;
using WorkPing.Features.AttendanceLog.ViewModels;
using WorkPing.Features.AttendanceLogViewer.ViewModels;
using WorkPing.Features.Shortcut.ViewModels;
using WorkPing.Services;

namespace WorkPing;

/// <summary>
/// アプリケーションのエントリーポイント。
/// DIコンテナのセットアップと起動処理を担う。
/// </summary>
public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;
    private MainWindow? _mainWindow;

    /// <summary>
    /// メインウィンドウの HWND。FileSavePicker などの Win32 API に渡すために使用する。
    /// MainWindow のコンストラクタで設定される。
    /// </summary>
    public static nint MainWindowHandle { get; set; }

    // ログはデスクトップではなく %TEMP% に書く（OneDrive 等の影響を受けない）
    private static readonly string TraceLogPath =
        Path.Combine(Path.GetTempPath(), "WorkPing_startup.log");
    private static readonly string CrashLogPath =
        Path.Combine(Path.GetTempPath(), "WorkPing_crash.log");

    public App()
    {
        Trace("=== App() START ===");
        try
        {
            InitializeComponent();
            Trace("InitializeComponent() OK");

            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            Trace("Exception handlers registered");

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
            Trace("DI container built");
        }
        catch (Exception ex)
        {
            Trace($"App() EXCEPTION: {ex}");
            throw;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SettingsService>();
        services.AddSingleton<AttendanceLogService>();
        services.AddSingleton<AccessCheckService>();
        services.AddSingleton<FileWatcherService>();
        services.AddSingleton<WindowsLoginService>();
        // NotificationService は登録だけしておき、Initialize() は呼ばない
        // （アンパッケージドアプリで AppNotificationManager が COM クラッシュを引き起こすため）
        services.AddSingleton<NotificationService>();

        services.AddTransient<AttendanceLogViewModel>();
        services.AddTransient<ShortcutViewModel>();
        services.AddTransient<AccountSettingsViewModel>();
        // ログ一覧ウィンドウはウィンドウごとに 1 インスタンス（Transient）
        services.AddTransient<AttendanceLogViewerViewModel>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Trace("OnLaunched() START");
        try
        {
            var settingsService = ServiceProvider.GetRequiredService<SettingsService>();
            Trace("SettingsService resolved");

            await settingsService.LoadSettingsAsync();
            Trace("LoadSettingsAsync() OK");

            // 通知サービスを初期化する
            // AppNotificationManager ではなく Windows.UI.Notifications.ToastNotificationManager を
            // 使用しているため、アンパッケージドアプリでも COM クラッシュなしに動作する
            var notificationService = ServiceProvider.GetRequiredService<NotificationService>();
            notificationService.Initialize();
            Trace("NotificationService.Initialize() OK");

            Trace("Creating MainWindow...");
            _mainWindow = new MainWindow();
            Trace("MainWindow created");

            _mainWindow.Activate();
            Trace("MainWindow.Activate() called");
        }
        catch (Exception ex)
        {
            Trace($"OnLaunched() EXCEPTION: {ex}");
            WriteCrashLog("OnLaunched", ex);
        }
    }

    // UI スレッドの未処理例外
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Trace($"UnhandledException: {e.Exception}");
        WriteCrashLog("UnhandledException", e.Exception);
        Environment.Exit(1);
    }

    // バックグラウンドスレッドの未処理例外
    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Trace($"DomainUnhandledException: {e.ExceptionObject}");
        WriteCrashLog("DomainUnhandledException", e.ExceptionObject as Exception);
    }

    /// <summary>起動トレースを %TEMP%\WorkPing_startup.log に追記する。</summary>
    public static void Trace(string message)
    {
        try
        {
            File.AppendAllText(TraceLogPath,
                $"{DateTime.Now:HH:mm:ss.fff}  {message}\n");
        }
        catch { }
    }

    private static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n" +
                       $"Type   : {ex?.GetType().FullName}\n" +
                       $"Message: {ex?.Message}\n" +
                       $"Stack  :\n{ex?.StackTrace}\n" +
                       $"Inner  : {ex?.InnerException?.Message}\n" +
                       new string('-', 60) + "\n";
            File.AppendAllText(CrashLogPath, text);
        }
        catch { }
    }
}
