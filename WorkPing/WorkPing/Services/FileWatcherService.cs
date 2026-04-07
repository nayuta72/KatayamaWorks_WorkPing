using Reactive.Bindings;
using WorkPing.Models;

namespace WorkPing.Services;

/// <summary>
/// 出退勤ログ XML ファイルの変更を監視するサービスクラス。
/// ファイルに書き込みが発生すると FileChanged イベントを発火する。
///
/// ※ 管理者ユーザー（IsAdmin = true）のみ有効な機能。
///    管理者でない場合は監視を開始しない。
///
/// 設定に登録されているすべての有効なログファイルパスを監視対象とする（最大3件）。
/// 設定が変更されたときは監視対象を自動的に再構築する。
/// </summary>
public class FileWatcherService : IDisposable
{
    private readonly SettingsService _settingsService;

    // ファイルパス → FileSystemWatcher のマップ
    // 監視対象が変わるたびにすべて作り直す
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();

    // デバウンス用タイマーのマップ（ファイルパスをキーにする）
    // FileSystemWatcher.Changed は1回の書き込みで複数回発火するため、
    // 500ms 以内に来た連続イベントを間引いて最後の1回だけ通知する
    private readonly Dictionary<string, Timer> _debounceTimers = new();

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

        // 設定が変更されるたびに監視対象を再構築する
        _settingsSubscription = _settingsService.Settings.Subscribe(settings =>
        {
            UpdateWatcher(settings);
        });
    }

    /// <summary>
    /// 現在の設定に基づいてファイル監視を開始・更新する。
    /// 管理者でない場合や有効なパスがない場合は監視しない。
    /// 設定に登録されているすべての有効なファイルパスを監視対象にする。
    /// </summary>
    public void UpdateWatcher(AppSettings settings)
    {
        // 既存のウォッチャーをすべて破棄して監視を停止する
        StopAllWatchers();

        // 管理者でない場合は監視しない
        if (!settings.IsAdmin) return;

        // 有効なパスを持つファイルをすべて監視対象にする
        foreach (var logFilePath in settings.LogFilePaths)
        {
            var path = logFilePath.FilePath;
            if (string.IsNullOrWhiteSpace(path)) continue;

            StartWatchingFile(path);
        }
    }

    /// <summary>
    /// 指定したファイルパスの監視を開始する。
    /// ディレクトリが存在しない場合や既に監視中の場合はスキップする。
    /// </summary>
    /// <param name="filePath">監視するファイルのフルパス</param>
    private void StartWatchingFile(string filePath)
    {
        // 既に同じパスを監視中であればスキップする
        if (_watchers.ContainsKey(filePath)) return;

        var directory = Path.GetDirectoryName(filePath);
        var fileName  = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) return;
        if (!Directory.Exists(directory)) return;

        try
        {
            var watcher = new FileSystemWatcher(directory, fileName)
            {
                // ファイルの最終更新日時またはサイズが変わったときに通知する
                NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnFileChanged;

            // デバウンスタイマーを生成する（初期は無効状態で待機させる）
            var timer = new Timer(OnDebounceElapsed, filePath,
                Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            _watchers[filePath]        = watcher;
            _debounceTimers[filePath]  = timer;

            System.Diagnostics.Debug.WriteLine($"[FileWatcherService] ファイル監視開始: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileWatcherService] 監視開始エラー ({filePath}): {ex.Message}");
        }
    }

    /// <summary>
    /// ファイルが変更されたときのハンドラ。
    /// FileSystemWatcher.Changed は1回の書き込みでも複数回発火するため、
    /// デバウンスタイマーで 500ms 以内の連続発火を間引き、最後の1回だけ通知する。
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var filePath = e.FullPath;

        // 対象ファイルのデバウンスタイマーをリセットして再スタートする
        if (_debounceTimers.TryGetValue(filePath, out var timer))
        {
            timer.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// デバウンスタイマーのコールバック。
    /// 最後の Changed イベントから 500ms 経過したタイミングで1回だけ呼ばれる。
    /// state にはファイルパスが格納されている。
    /// </summary>
    private void OnDebounceElapsed(object? state)
    {
        if (state is not string path) return;

        System.Diagnostics.Debug.WriteLine($"[FileWatcherService] ファイル変更通知: {path}");
        FileChanged?.Invoke(this, path);
    }

    /// <summary>
    /// すべてのウォッチャーとデバウンスタイマーを停止・破棄する。
    /// </summary>
    private void StopAllWatchers()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileChanged;
            watcher.Dispose();
        }
        _watchers.Clear();

        foreach (var timer in _debounceTimers.Values)
        {
            timer.Dispose();
        }
        _debounceTimers.Clear();
    }

    public void Dispose()
    {
        StopAllWatchers();
        _settingsSubscription?.Dispose();
    }
}
