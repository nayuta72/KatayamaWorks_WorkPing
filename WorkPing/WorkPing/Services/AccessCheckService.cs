using Reactive.Bindings;
using System.Diagnostics;
using WorkPing.Models;

namespace WorkPing.Services;

/// <summary>
/// ログ XML ファイルへのアクセス可否を定期的（5分ごと）に確認するサービスクラス。
///
/// 主な役割：
/// 1. ファイルアクセス可否を IsFileAccessible として通知する
///    → MainWindow がこれを購読してタイトルバーに警告アイコンを表示する
/// 2. アクセス不可時に書き込みをキューに保留し、
///    アクセス可能になったタイミングで自動的に書き込みを再実行する
/// 3. 書き込み失敗時に HasPendingWrite フラグを settings.json に永続化する
///    → アプリを再起動してもキューが復元され、書き込み漏れを防ぐ
/// </summary>
public class AccessCheckService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly AttendanceLogService _attendanceLogService;

    /// <summary>
    /// 現在選択中のファイルへのアクセス可否を表すリアクティブプロパティ。
    /// true = アクセス可能、false = アクセス不可。
    /// 書き込みキュー（EnqueueWriteAsync）の即時書き込み判定に使用する。
    /// </summary>
    public ReactivePropertySlim<bool> IsFileAccessible { get; } = new(true);

    /// <summary>
    /// アクセスできないファイルの表示名（拡張子なしファイル名）リスト。
    /// 登録されているすべてのファイルを確認し、アクセス不可のものを列挙する。
    /// MainWindow がこれを購読してタイトルバーの警告アイコンとファイル名を表示する。
    /// 空リストの場合はすべてのファイルにアクセス可能。
    /// </summary>
    public ReactivePropertySlim<IReadOnlyList<string>> InaccessibleFileNames { get; }
        = new(Array.Empty<string>());

    // アクセス不可時に書き込みを一時的に溜めるキュー（同一セッション内での保留に使用）
    // アプリ再起動後の保留は settings.json の HasPendingWrite フラグで管理する
    private readonly Queue<(AttendanceEntry Entry, string? FilePath)> _pendingEntries = new();

    // 定期チェック用タイマー（5分ごとに CheckAndFlushAsync を呼ぶ）
    private readonly System.Threading.Timer _checkTimer;

    // チェック間隔：5分（ミリ秒）
    private const int CheckIntervalMs = 5 * 60 * 1000;

    public AccessCheckService(SettingsService settingsService, AttendanceLogService attendanceLogService)
    {
        _settingsService      = settingsService;
        _attendanceLogService = attendanceLogService;

        // 起動 1 秒後に最初のチェックを行い、以降 5 分ごとにチェックする
        // 起動時チェックで HasPendingWrite の復元も行う
        _checkTimer = new System.Threading.Timer(
            async _ => await CheckAndFlushAsync(),
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(CheckIntervalMs));
    }

    /// <summary>
    /// 出退勤エントリーの書き込みをリクエストする。
    /// アクセス可能であれば即時書き込み、不可であればキューに保留する。
    /// 書き込み失敗時は settings.json の HasPendingWrite フラグを true にして永続化する
    /// （アプリ終了・再起動後も書き込み漏れが発生しないようにするため）。
    /// </summary>
    /// <param name="entry">書き込む出退勤エントリー</param>
    /// <param name="filePath">対象ファイルパス（null の場合は設定から取得）</param>
    public async Task EnqueueWriteAsync(AttendanceEntry entry, string? filePath = null)
    {
        if (IsFileAccessible.Value)
        {
            try
            {
                await _attendanceLogService.WriteEntryAsync(entry, filePath);
                // 書き込み成功 → 保留フラグが残っていればクリアする
                await ClearPendingFlagIfNeededAsync();
                return;
            }
            catch
            {
                // 書き込み失敗 → アクセス不可状態に切り替える
                IsFileAccessible.Value = false;
            }
        }

        // アクセス不可の場合はキューに保留し、settings.json に失敗フラグを永続化する
        // → アプリを終了・再起動しても次回起動時に再書き込みが試みられる
        _pendingEntries.Enqueue((entry, filePath));
        await PersistPendingFlagAsync();
        Debug.WriteLine($"[AccessCheckService] 書き込みを保留しました。キュー数: {_pendingEntries.Count}");
    }

    /// <summary>
    /// ファイルアクセスを確認し、可能であれば保留キューの書き込みを実行する。
    /// このメソッドは Timer から 5 分ごとに自動的に呼ばれる。
    ///
    /// アプリ再起動後の処理：
    ///   起動 1 秒後のタイマー発火時に HasPendingWrite を検出し、
    ///   settings.json から最新の出退勤データを復元してキューに積む。
    ///   最新の出勤・退勤データを 1 エントリーとしてまとめて書き込むため、
    ///   再起動をまたいでも正しく上書きされる。
    /// </summary>
    private async Task CheckAndFlushAsync()
    {
        var settings    = _settingsService.Settings.Value;
        var currentPath = settings.CurrentLogFilePath?.FilePath;

        // ─── 全登録ファイルのアクセス確認 ────────────────────────────────────
        // 登録されているすべての有効なパスを確認し、アクセス不可のファイル名を収集する
        var inaccessibleNames = new List<string>();
        foreach (var logFilePath in settings.LogFilePaths)
        {
            var path = logFilePath.FilePath;
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (!CheckFileAccess(path))
            {
                // 表示名（拡張子なしファイル名）を収集する
                inaccessibleNames.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        // アクセス不可ファイル名リストを更新する（変化があった場合のみ通知）
        InaccessibleFileNames.Value = inaccessibleNames;

        // ─── 選択中ファイルのアクセス状態を更新（書き込みキュー用） ─────────
        // ログパスが設定されていない場合はチェックをスキップする
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            IsFileAccessible.Value = true;
            return;
        }

        var isAccessible = !inaccessibleNames.Contains(
            Path.GetFileNameWithoutExtension(currentPath));
        IsFileAccessible.Value = isAccessible;

        // アクセス不可の場合は何もしない
        if (!isAccessible) return;

        // ─── 再起動後の保留復元 ──────────────────────────────────────────────
        // キューが空でも HasPendingWrite フラグが立っている場合、
        // settings.json から最新の出退勤データを復元してキューに積む
        if (settings.InternalState.HasPendingWrite && _pendingEntries.Count == 0)
        {
            var restored = ReconstructEntryFromSettings(settings);
            if (restored != null)
            {
                _pendingEntries.Enqueue((restored, null));
                Debug.WriteLine("[AccessCheckService] 再起動後の保留エントリーを settings から復元しました。");
            }
        }

        // ─── キューのフラッシュ ──────────────────────────────────────────────
        if (_pendingEntries.Count == 0) return;

        Debug.WriteLine($"[AccessCheckService] アクセス回復。保留 {_pendingEntries.Count} 件を書き込みます。");

        while (_pendingEntries.TryDequeue(out var pending))
        {
            try
            {
                await _attendanceLogService.WriteEntryAsync(pending.Entry, pending.FilePath);
            }
            catch
            {
                // 再度失敗した場合はキューに戻して終了する
                _pendingEntries.Enqueue(pending);
                IsFileAccessible.Value = false;
                Debug.WriteLine("[AccessCheckService] 書き込み再失敗。再度保留します。");
                return;
            }
        }

        // キューが空になった（全件書き込み成功）→ 保留フラグをクリアする
        await ClearPendingFlagIfNeededAsync();
        Debug.WriteLine("[AccessCheckService] 保留キューを全件書き込み完了。HasPendingWrite をクリアしました。");
    }

    /// <summary>
    /// settings.json の HasPendingWrite フラグを true に設定して保存する。
    /// 書き込み失敗時に呼び出す。
    /// </summary>
    private async Task PersistPendingFlagAsync()
    {
        try
        {
            _settingsService.Settings.Value.InternalState.HasPendingWrite = true;
            await _settingsService.SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            // settings の保存自体が失敗した場合はログだけ残してアプリを落とさない
            Debug.WriteLine($"[AccessCheckService] HasPendingWrite=true の保存に失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// settings.json の HasPendingWrite フラグが true の場合に false へクリアして保存する。
    /// 書き込み成功後に呼び出す。
    /// </summary>
    private async Task ClearPendingFlagIfNeededAsync()
    {
        try
        {
            if (!_settingsService.Settings.Value.InternalState.HasPendingWrite) return;
            _settingsService.Settings.Value.InternalState.HasPendingWrite = false;
            await _settingsService.SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AccessCheckService] HasPendingWrite=false の保存に失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// settings.json に保存されている最新の出退勤データから AttendanceEntry を再構築する。
    /// アプリ再起動後の保留エントリー復元に使用する。
    ///
    /// 最新の出勤データと最新の退勤データを 1 つのエントリーにまとめるため、
    /// XML への書き込みは「同じ日付・ユーザーIDのエレメントを上書き更新」になる。
    /// </summary>
    /// <returns>再構築したエントリー。必須項目が不足している場合は null。</returns>
    private static AttendanceEntry? ReconstructEntryFromSettings(AppSettings settings)
    {
        // 必須項目が揃っていない場合は復元不可
        var state = settings.InternalState;

        if (string.IsNullOrEmpty(state.LastLogDate)
            || string.IsNullOrEmpty(settings.FullName))
        {
            Debug.WriteLine("[AccessCheckService] 必須項目が不足しているため保留エントリーを復元できませんでした。");
            return null;
        }

        // 退勤時刻があれば ClockOut アクション、なければ ClockIn アクションとして復元する
        var action = string.IsNullOrEmpty(state.TodayClockOutTime)
            ? AttendanceAction.ClockIn
            : AttendanceAction.ClockOut;

        return new AttendanceEntry
        {
            Date           = state.LastLogDate,
            Name           = settings.FullName,
            // 勤務形態が未設定の場合は "出社" をデフォルト値にする
            WorkType       = string.IsNullOrEmpty(state.TodayWorkType) ? "出社" : state.TodayWorkType,
            // null や空文字は「未入力」として扱い、XML 属性の更新をスキップさせる
            ClockInTime    = string.IsNullOrEmpty(state.TodayClockInTime)    ? null : state.TodayClockInTime,
            ClockInStatus  = string.IsNullOrEmpty(state.TodayClockInStatus)  ? null : state.TodayClockInStatus,
            ClockOutTime   = string.IsNullOrEmpty(state.TodayClockOutTime)   ? null : state.TodayClockOutTime,
            ClockOutStatus = string.IsNullOrEmpty(state.TodayClockOutStatus) ? null : state.TodayClockOutStatus,
            Action         = action,
        };
    }

    /// <summary>
    /// 指定したファイルパスへのアクセス可否を確認する。
    /// ファイルを実際に開いて確認するため、ネットワーク共有のアクセス不可も検出できる。
    /// </summary>
    private static bool CheckFileAccess(string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);

            // ディレクトリが存在しない場合はアクセス不可
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) return false;

            // ファイルが存在しない場合はアクセス不可と判断する
            // （自動作成はしない。ファイルはアカウント設定の「新規作成」ボタンで明示的に作成する）
            if (!File.Exists(filePath)) return false;

            // 実際にファイルを開いて書き込み可否を確認する
            using var fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 即時アクセスチェックを実行する。
    /// 設定保存など、5分待たずにすぐ結果を反映したい場合に呼び出す。
    /// </summary>
    public async Task CheckNowAsync() => await CheckAndFlushAsync();

    public void Dispose()
    {
        _checkTimer?.Dispose();
        IsFileAccessible.Dispose();
        InaccessibleFileNames.Dispose();
    }
}
