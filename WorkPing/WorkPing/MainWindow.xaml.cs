using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WorkPing.Features.AccountSettings.Views;
using WorkPing.Features.AttendanceLog.Views;
using WorkPing.Features.AttendanceLogViewer.Views;
using WorkPing.Features.Shortcut.Views;
using WorkPing.Models;
using WorkPing.Services;

namespace WorkPing;

/// <summary>
/// メインウィンドウ。
/// NavigationView で 3 ページを管理し、カスタムタイトルバーに
/// ログファイル切り替えコンボボックスを配置する。
/// </summary>
public sealed partial class MainWindow : Window
{
    // ===========================
    // 依存するサービス
    // ===========================
    private readonly SettingsService _settingsService;
    private readonly AccessCheckService _accessCheckService;
    private readonly FileWatcherService _fileWatcherService;
    private readonly NotificationService _notificationService;

    // ===========================
    // 勤怠ログ一覧ウィンドウ
    // ===========================
    // モードレスウィンドウのインスタンス（null = 未表示）
    private AttendanceLogViewerWindow? _logViewerWindow;
    // 現在選択されている日付範囲（SplitButton ドロップダウンで変更される）
    private string _logViewerDateRange = "Today";

    public MainWindow()
    {
        App.Trace("MainWindow() constructor start");
        InitializeComponent();
        App.Trace("MainWindow InitializeComponent OK");

        _settingsService     = App.ServiceProvider.GetRequiredService<SettingsService>();
        _accessCheckService  = App.ServiceProvider.GetRequiredService<AccessCheckService>();
        _fileWatcherService  = App.ServiceProvider.GetRequiredService<FileWatcherService>();
        _notificationService = App.ServiceProvider.GetRequiredService<NotificationService>();
        App.Trace("MainWindow services resolved");

        // FileSavePicker などの Win32 API に渡すための HWND を保存する
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        App.MainWindowHandle = hwnd;
        App.Trace("MainWindowHandle set");

        InitializeTitleBar();
        App.Trace("InitializeTitleBar OK");

        SetWindowSize(870, 370);
        App.Trace("SetWindowSize OK");

        // 退勤ステータス未入力のときに閉じようとしたら確認ダイアログを表示する
        var appWindow = GetAppWindow();
        appWindow.Closing += AppWindow_Closing;

        Activated += MainWindow_Activated;
        App.Trace("MainWindow() constructor done");
    }

    /// <summary>
    /// ウィンドウがアクティブになったタイミングで初回処理を行う。
    /// Activated イベントを利用して「ウィンドウ表示後」に実行されることを保証する。
    /// </summary>
    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        App.Trace("MainWindow_Activated start");
        try
        {
            InitializeLogFileComboBox();
            App.Trace("InitializeLogFileComboBox OK");

            SubscribeAccessStatus();
            App.Trace("SubscribeAccessStatus OK");

            SubscribeSettingsChanges();
            App.Trace("SubscribeSettingsChanges OK");

            SubscribeFileWatcher();
            App.Trace("SubscribeFileWatcher OK");

            NavigateToPage("AttendanceLog");
            App.Trace("NavigateToPage OK");

            MainNavView.SelectedItem = AttendanceLogNavItem;

            await ValidateAndNavigateToSettingsAsync();
            App.Trace("ValidateAndNavigateToSettings OK");
        }
        catch (Exception ex)
        {
            App.Trace($"MainWindow_Activated EXCEPTION: {ex}");
        }
    }

    // ===========================
    // タイトルバー初期化
    // ===========================

    /// <summary>
    /// カスタムタイトルバーを設定する。
    /// ExtendsContentIntoTitleBar = true にして、AppTitleBar グリッドをドラッグ領域として登録する。
    /// また、タスクバー・Alt+Tab・タイトルバー左上に表示するウィンドウアイコンを設定する。
    /// </summary>
    private void InitializeTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // ウィンドウアイコンを設定する（タスクバー・Alt+Tab・タイトルバー左上）
        // パスは exe と同じフォルダからの相対パスで指定する
        GetAppWindow().SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "kintai.ico"));
    }

    // ===========================
    // ウィンドウサイズ設定
    // ===========================
    private void SetWindowSize(int width, int height)
    {
        GetAppWindow().Resize(new SizeInt32(width, height));
    }

    /// <summary>このウィンドウに対応する AppWindow を返すヘルパー。</summary>
    private AppWindow GetAppWindow()
    {
        var hwnd     = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    // ===========================
    // ウィンドウを閉じるときの確認
    // ===========================

    // ユーザーが確認ダイアログで「閉じる」を選んだ後の再クローズかどうかを示すフラグ。
    // Closing イベントは Close() を呼んだときにも発火するため、
    // 無限ループを防ぐためにこのフラグで一度確認済みかどうかを管理する。
    private bool _isCloseConfirmed = false;

    /// <summary>
    /// ウィンドウを閉じようとしたときに呼ばれる。
    /// 退勤ステータスが未入力の場合、確認ダイアログを表示して
    /// ユーザーが「閉じる」を選んだときのみウィンドウを閉じる。
    ///
    /// GetDeferral() が使用できないため、以下のパターンで実装する：
    ///   1. args.Cancel = true でいったんクローズをキャンセルする
    ///   2. 非同期でダイアログを表示する
    ///   3. 「閉じる」が選ばれたら _isCloseConfirmed = true にして Close() を再呼び出し
    ///   4. 再発火した Closing イベントでは _isCloseConfirmed = true のためスキップされる
    /// </summary>
    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // 確認済みの再クローズの場合はそのまま閉じる（無限ループ防止）
        if (_isCloseConfirmed) return;

        // 今日の退勤ステータスが入力済みかどうかを確認する
        var settings = _settingsService.Settings.Value;
        var todayStr = DateTime.Today.ToString("yyyyMMdd");
        var isToday     = settings.InternalState.LastLogDate == todayStr;
        var hasClockOut = isToday && !string.IsNullOrEmpty(settings.InternalState.TodayClockOutStatus);

        // 退勤ステータスが入力済みなら確認不要でそのまま閉じる
        if (hasClockOut) return;

        // XamlRoot が設定されていない場合（ウィンドウがまだ表示されていないなど）は
        // ダイアログを表示できないのでそのまま閉じる
        if (Content?.XamlRoot == null) return;

        // いったんクローズをキャンセルしてダイアログの結果を待つ
        args.Cancel = true;

        var dialog = new ContentDialog
        {
            Title             = "退勤ステータスが未入力です",
            Content           = "退勤ステータスのボタンがまだ押されていません。\nこのまま閉じてもよいですか？",
            PrimaryButtonText = "閉じる",
            CloseButtonText   = "キャンセル",
            DefaultButton     = ContentDialogButton.Close,
            XamlRoot          = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        // 「閉じる」が選ばれた場合は確認フラグを立てて Close() を再呼び出しする
        if (result == ContentDialogResult.Primary)
        {
            _isCloseConfirmed = true;
            this.Close();
        }
    }

    // ===========================
    // ログファイルコンボボックス
    // ===========================

    /// <summary>
    /// 設定に登録されているログファイルパスをコンボボックスに反映する。
    /// アカウント設定ページで保存が行われた後にも呼び出す。
    /// また、コンボボックスとログ一覧ボタンの表示・非表示もここで更新する。
    /// - コンボボックス：パスが2件以上のときのみ表示
    /// - ログ一覧ボタン：管理者のときのみ表示
    /// </summary>
    private void InitializeLogFileComboBox()
    {
        var settings      = _settingsService.Settings.Value;
        var mainIndex     = settings.InternalState.MainLogFileIndex;
        LogFileComboBox.Items.Clear();

        // パスが2件以上あるときのみコンボボックスを表示する
        // （1件以下の場合は選択肢がないため非表示にする）
        if (settings.LogFilePaths.Count >= 2)
        {
            for (var i = 0; i < settings.LogFilePaths.Count; i++)
            {
                var logPath   = settings.LogFilePaths[i];
                // メインファイルかどうかをフラグにセットする（★表示の制御に使用）
                logPath.IsMain = (i == mainIndex);
                LogFileComboBox.Items.Add(logPath);
            }

            // 起動時はメインファイルを選択状態にする
            LogFileComboBox.SelectedIndex =
                mainIndex < LogFileComboBox.Items.Count ? mainIndex : 0;

            LogFileComboBox.Visibility = Visibility.Visible;
        }
        else
        {
            // パスが0または1件のとき：コンボボックスを非表示にする
            if (settings.LogFilePaths.Count == 1)
            {
                settings.LogFilePaths[0].IsMain = true;
                LogFileComboBox.Items.Add(settings.LogFilePaths[0]);
                LogFileComboBox.SelectedIndex = 0;
            }
            LogFileComboBox.Visibility = Visibility.Collapsed;
        }

        // ログ一覧ボタンは管理者のみ表示する
        LogViewerDropDownButton.Visibility = settings.IsAdmin
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// コンボボックスで選択されたログファイルが変わっても設定は保存しない。
    /// コンボの選択はどのファイルを閲覧・監視するかの UI 状態であり、
    /// ログの書き込み先（メインファイル）はアカウント設定で管理する。
    /// </summary>
    private void LogFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 選択変更では settings を更新しない（意図的な空ハンドラ）
    }

    // ===========================
    // アクセス状態インジケーター
    // ===========================

    /// <summary>
    /// アクセス不可ファイル名リストの変化を購読し、タイトルバーの警告表示を更新する。
    /// リストが空 = すべてアクセス可能 → パネルを非表示
    /// リストに1件以上 = アクセス不可あり → パネルを表示してファイル名を並べる
    /// </summary>
    private void SubscribeAccessStatus()
    {
        _accessCheckService.InaccessibleFileNames.Subscribe(names =>
        {
            // UIスレッドで実行する（購読はバックグラウンドスレッドから呼ばれる可能性がある）
            DispatcherQueue.TryEnqueue(() =>
            {
                if (names.Count == 0)
                {
                    AccessWarningPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // アクセス不可のファイル名をカンマ区切りで表示する（例: "Log2025, Log2024_B"）
                    AccessWarningFileNames.Text   = string.Join(", ", names);
                    AccessWarningPanel.Visibility = Visibility.Visible;
                }
            });
        });
    }

    // ===========================
    // 設定変更の購読（管理者フラグ切り替えの即時反映）
    // ===========================

    /// <summary>
    /// アカウント設定の保存完了時に NotifySettingsChanged() が呼ばれると発火する。
    /// IsAdmin の変更を再起動なしで即時反映するため、
    /// ログ一覧ボタンの表示状態をここで更新する。
    /// </summary>
    private void SubscribeSettingsChanges()
    {
        _settingsService.Settings.Subscribe(_ =>
        {
            // Subscribe は別スレッドから呼ばれる可能性があるため UI スレッドに戻す
            DispatcherQueue.TryEnqueue(() =>
            {
                InitializeLogFileComboBox();
            });
        });
    }

    // ===========================
    // ファイル変更通知（管理者のみ）
    // ===========================

    /// <summary>
    /// ログXMLファイルの変更を購読し、LastLogの内容をトースト通知で表示する。
    /// 管理者フラグが true のときのみ機能する。
    /// </summary>
    private void SubscribeFileWatcher()
    {
        _fileWatcherService.FileChanged += async (sender, filePath) =>
        {
            // 管理者のみ通知する
            if (!_settingsService.Settings.Value.IsAdmin) return;

            // ファイルからLastLogを読み取って通知する
            var attendanceLogService = App.ServiceProvider.GetRequiredService<AttendanceLogService>();
            var lastLog = await attendanceLogService.GetLastLogAsync(filePath);
            if (lastLog == null) return;

            var name           = lastLog.Attribute("Name")?.Value           ?? "不明";
            var comment        = lastLog.Attribute("Comment")?.Value;
            var clockIn        = lastLog.Attribute("ClockIn")?.Value;
            var clockOut       = lastLog.Attribute("ClockOut")?.Value;
            var clockInStatus  = lastLog.Attribute("ClockInStatus")?.Value;
            var clockOutStatus = lastLog.Attribute("ClockOutStatus")?.Value;

            // LastLog に書き込まれた属性の有無でアクション種別を判定する
            // Comment属性あり → コメント送信 / ClockOut属性あり → 退勤 / それ以外 → 出勤
            string message;
            string? statusForIcon;

            if (!string.IsNullOrEmpty(comment))
            {
                // コメント通知：吹き出しアイコン + 名前とコメント本文を表示する
                message       = $"{name}: {comment}";
                statusForIcon = StatusIconService.CommentKey;
            }
            else if (!string.IsNullOrEmpty(clockOut))
            {
                message       = $"{name} が退勤しました";
                statusForIcon = clockOutStatus;
            }
            else
            {
                message       = $"{name} が出勤しました";
                statusForIcon = clockInStatus;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                _notificationService.ShowNotification("管理者へ", message, statusForIcon);
            });
        };
    }

    // ===========================
    // 設定検証
    // ===========================

    /// <summary>
    /// 起動時に設定の必須項目を検証する。
    /// 未設定がある場合はアカウント設定ページに自動的に遷移してユーザーに設定を促す。
    /// </summary>
    private async Task ValidateAndNavigateToSettingsAsync()
    {
        if (_settingsService.IsSettingsValid()) return;

        // Activated イベント直後は XamlRoot がまだ設定されていない場合があるため、
        // 設定されるまで待機してからダイアログを表示する。
        // ※ Task.Yield() は DispatcherQueue に即座に再キューするため飽和を引き起こし、
        //   コンポジター初期化が進まず XamlRoot が永久に null になるデッドロックが発生する。
        //   Task.Delay で間隔を空けることでコンポジター側の処理に順番を譲れる。
        for (int i = 0; i < 20 && Content?.XamlRoot == null; i++)
            await Task.Delay(100);

        // 2 秒待っても XamlRoot が設定されない場合はダイアログを出さずに終了する
        if (Content?.XamlRoot == null) return;

        // 案内ダイアログを表示してからアカウント設定ページへ遷移する
        var dialog = new ContentDialog
        {
            Title = "アカウント設定が必要です",
            Content = "姓・名・ユーザーID・出退勤ログファイルパスが設定されていません。\nアカウント設定画面で情報を入力してください。",
            PrimaryButtonText = "設定画面へ",
            CloseButtonText = "後で",
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            NavigateToPage("AccountSettings");
            MainNavView.SelectedItem = AccountSettingsNavItem;
        }
    }

    // ===========================
    // 勤怠ログ一覧ウィンドウ（DropDownButton）
    // ===========================

    /// <summary>
    /// DropDownButton のメニューアイテムクリック：日付範囲を変更してウィンドウを開く。
    /// ウィンドウがすでに開いている場合は日付範囲を更新して前面に出す。
    /// ウィンドウが閉じている場合は新規作成して開く。
    /// </summary>
    private async void LogViewerRange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item) return;
        var range = item.Tag?.ToString() ?? "Today";

        // 日付範囲をボタンラベルとフィールドに反映する
        _logViewerDateRange = range;
        LogViewerRangeLabel.Text = range;

        // 現在選択中のログファイルパスを取得する
        var filePath = _settingsService.Settings.Value.CurrentLogFilePath?.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            var dialog = new ContentDialog
            {
                Title           = "ログファイル未設定",
                Content         = "ログファイルが設定されていません。\nアカウント設定でパスを登録してください。",
                CloseButtonText = "閉じる",
                XamlRoot        = Content.XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        if (_logViewerWindow == null)
        {
            // 新規作成して開く
            _logViewerWindow = new AttendanceLogViewerWindow();
            _logViewerWindow.Closed += (s, _) => _logViewerWindow = null;
            _logViewerWindow.Activate();
            await _logViewerWindow.LoadAsync(filePath, range);
        }
        else
        {
            // すでに開いているので日付範囲を更新して前面に出す
            _logViewerWindow.ChangeDateRange(range);
            _logViewerWindow.Activate();
        }
    }

    // ===========================
    // ナビゲーション
    // ===========================

    /// <summary>
    /// NavigationView のアイテムが選択されたときにページ遷移する。
    /// アカウント設定ページから戻るときにコンボボックスを更新する（設定が変わった可能性があるため）。
    /// </summary>
    private void MainNavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item)
        {
            // アカウント設定ページから他のページへ遷移するときにコンボボックスを更新する
            if (ContentFrame.CurrentSourcePageType == typeof(AccountSettingsPage)
                && item.Tag?.ToString() != "AccountSettings")
            {
                InitializeLogFileComboBox();
            }

            NavigateToPage(item.Tag?.ToString());
        }
    }

    /// <summary>
    /// タグ名に対応するページに遷移する。
    /// </summary>
    private void NavigateToPage(string? tag)
    {
        var pageType = tag switch
        {
            "AttendanceLog"   => typeof(AttendanceLogPage),
            "Shortcut"        => typeof(ShortcutPage),
            "AccountSettings" => typeof(AccountSettingsPage),
            _                 => null
        };

        if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
