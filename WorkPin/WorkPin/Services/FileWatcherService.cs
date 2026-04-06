using Reactive.Bindings;
using WorkPin.Models;

namespace WorkPin.Services;

/// <summary>
/// 出退勤ログ XML ファイルの変更を監視するサービスクラス。
/// ファイルに書き込みが発生すると FileChanged イベントを発火する。
///
/// ※ 管理者ユーザー（IsAdmin = true）のみ有効な機能。
///    管理者でない場合は監視を開始しない。
///
/// 設定が変更されたとき（ログファイルパスが変わったとき）に
/// 監視対象を自動的に切り替える。
/// </summary>
public class FileWatcherService : IDisposable
{
    private readonly SettingsService _settingsService;

    // FileSystemWatcher のインスタンス（監視対象ファイルが変わるたびに作り直す）
    private FileSystemWatcher? _watcher;

    // デバウンス用タイマー
    // FileSystemWatcher.Changed は1回の書き込みで複数回発火するため、
    // 500ms 以内に来た連続イベントを間引いて最後の1回だけ通知する
    private Timer? _debounceTimer;
    private string? _pendingFilePath;
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(500);

    // 設定変更の購読を保持（Dispose 時に解除する）
    private IDisposable? _settingsSubscription;

    /// <summary>
    /// ファイル変更が検出されたときに発火するイベント。
    /// 引数は変更されたファイルのフルパス。
    /// MainWindow がこれを購読してトースト通知を送信する。
    /// </summary>
    public event EventHandler<string>? FileChanged;

    public FileWatcherService(SettingsService settingsService)
    {
        _settingsService = settingsService;

        // 設定が変更されるたびに監視対象を更新する
        _settingsSubscription = _settingsService.Settings.Subscribe(settings =>
        {
            UpdateWatcher(settings);
        });
    }

    /// <summary>
    /// 現在の設定に基づいてファイル監視を開始・更新する。
    /// 管理者でない場合や設定が不正な場合は監視しない。
    /// </summary>
    public void UpdateWatcher(AppSettings settings)
    {
        // 既存のウォッチャーを破棄して監視を停止する
        StopWatcher();

        // 管理者でない場合は監視しない
        if (!settings.IsAdmin) return;

        var currentPath = settings.CurrentLogFilePath?.FilePath;
        if (string.IsNullOrWhiteSpace(currentPath)) return;

        var directory = Path.GetDirectoryName(currentPath);
        var fileName  = Path.GetFileName(currentPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) return;
        if (!Directory.Exists(directory)) return;

        try
        {
            _watcher = new FileSystemWatcher(directory, fileName)
            {
                // ファイルの最終更新日時またはサイズが変わったときに通知する
                NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;

            // デバウンスタイマーを生成する（初期は無効状態で待機させる）
            _debounceTimer = new Timer(OnDebounceElapsed, null,
                Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            System.Diagnostics.Debug.WriteLine($"[FileWatcherService] ファイル監視開始: {currentPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileWatcherService] 監視開始エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// ファイルが変更されたときのハンドラ。
    /// FileSystemWatcher.Changed は1回の書き込みでも複数回発火するため、
    /// デバウンスタイマーで 500ms 以内の連続発火を間引き、最後の1回だけ通知する。
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // 最新のファイルパスを保持し、タイマーをリセットして再スタートする
        _pendingFilePath = e.FullPath;

        _debounceTimer?.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// デバウンスタイマーのコールバック。
    /// 最後の Changed イベントから 500ms 経過したタイミングで1回だけ呼ばれる。
    /// </summary>
    private void OnDebounceElapsed(object? state)
    {
        var path = _pendingFilePath;
        if (path == null) return;

        System.Diagnostics.Debug.WriteLine($"[FileWatcherService] ファイル変更通知: {path}");
        FileChanged?.Invoke(this, path);
    }

    // 現在のウォッチャーとデバウンスタイマーを停止・破棄する
    private void StopWatcher()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer  = null;
        _pendingFilePath = null;
    }

    public void Dispose()
    {
        StopWatcher();
        _settingsSubscription?.Dispose();
    }
}
