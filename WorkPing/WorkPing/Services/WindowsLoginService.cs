using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;

namespace WorkPing.Services;

/// <summary>
/// Windows のログイン・ログアウト時刻を取得するサービスクラス。
///
/// 取得方針：
///   【今日のログイン時刻】
///     explorer.exe の起動時刻を使用する。
///     → 管理者権限不要・即時取得・ハングしない。
///
///   【前回起動日のログイン/ログアウト時刻】
///     System イベントログ（一般ユーザーでも読み取り可能）を使用する。
///
///     ① 前回起動日を特定するため、今日より前で最新の 6005 を1件取得する
///     ② 前回起動日の「最初の 6005」= その日の最初のログイン時刻
///     ③ 前回起動日の「最後の 6006」= シャットダウン時刻（クリーンシャットダウン）
///        取得できない場合は「最後の 1074」= ユーザー操作によるシャットダウン要求 でフォールバック
///        （Windows 11 の高速スタートアップ環境では 6006 が記録されないことがあるため）
///
///     複数回の再起動がある日でも最初のログインと最後のログアウトを取得できる。
///
///   使用イベント ID（System ログ）：
///     6005 : Event Log サービス開始 = システム起動 ≒ ログイン時刻
///     6006 : Event Log サービス停止 = クリーンシャットダウン ≒ ログアウト時刻
///     1074 : ユーザー/プロセスによるシャットダウン要求（高速スタートアップ環境でのフォールバック）
/// </summary>
public class WindowsLoginService
{
    // ===========================
    // 公開メソッド
    // ===========================

    /// <summary>
    /// 今日の最初のログイン時刻を取得する。
    /// explorer.exe の最も古い起動時刻を返す（即時取得）。
    /// </summary>
    public DateTime? GetTodayFirstLoginTime()
    {
        try
        {
            return Process.GetProcessesByName("explorer")
                .Select(p =>
                {
                    try { return (DateTime?)p.StartTime; }
                    catch { return null; }
                })
                .Where(t => t.HasValue && t.Value.Date == DateTime.Today)
                .OrderBy(t => t)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsLoginService] GetTodayFirstLoginTime エラー: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 前回起動日のログイン時刻とログアウト時刻を取得する。
    /// 「前回起動日」＝今日より前に最後にシステムが起動した日。
    /// System イベントログ（一般ユーザーで読み取り可能）を使用する。
    /// </summary>
    public async Task<(DateTime? LoginTime, DateTime? LogoutTime)> GetPreviousBootDayTimesAsync()
    {
        // Task.WhenAny でタイムアウトを設ける（System ログは通常数秒で読めるが念のため）
        var readTask    = Task.Run(() => ReadPreviousBootDayTimes());
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

        var completed = await Task.WhenAny(readTask, timeoutTask);
        if (completed != readTask)
        {
            Debug.WriteLine("[WindowsLoginService] 前回起動日の取得がタイムアウトしました。");
            return (null, null);
        }

        try { return await readTask; }
        catch { return (null, null); }
    }

    // ===========================
    // 内部実装
    // ===========================

    /// <summary>
    /// System イベントログから前回起動日の最初のログイン時刻と最後のログアウト時刻を取得する。
    ///
    /// 処理の流れ：
    ///   Step1. 今日より前で最新の 6005 を1件取得して「前回起動日」を特定する
    ///   Step2. 前回起動日全体を範囲とする日時文字列を生成する
    ///   Step3. 前回起動日の「最初の 6005」を取得（昇順 → 先頭1件）
    ///   Step4. 前回起動日の「最後の 6006」を取得（降順 → 先頭1件）
    ///   Step5. 6006 が取得できない場合は「最後の 1074」でフォールバック
    /// </summary>
    private static (DateTime? LoginTime, DateTime? LogoutTime) ReadPreviousBootDayTimes()
    {
        try
        {
            var todayUtc  = DateTime.Today.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
            var cutoffUtc = DateTime.Today.AddDays(-60).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

            // ─── Step1 ──────────────────────────────────────────────────────────
            // 今日より前で最も新しい 起動（6005）イベントを1件取得して「前回起動日」を特定する
            // ReverseDirection = true で最新順に検索し、先頭1件を読むだけで済む（高速）
            var latestBootQuery = new EventLogQuery("System", PathType.LogName,
                $"*[System[(EventID=6005) and TimeCreated[@SystemTime >= '{cutoffUtc}' and @SystemTime < '{todayUtc}']]]")
            {
                ReverseDirection = true
            };

            DateTime? latestBootTime = null;
            using (var reader = new EventLogReader(latestBootQuery))
            {
                var rec = reader.ReadEvent();
                if (rec != null)
                    using (rec) latestBootTime = rec.TimeCreated?.ToLocalTime();
            }

            // 1件も見つからなければ前回起動日が特定できないため終了
            if (!latestBootTime.HasValue)
            {
                Debug.WriteLine("[WindowsLoginService] 前回起動日の 6005 イベントが見つかりませんでした。");
                return (null, null);
            }

            // ─── Step2 ──────────────────────────────────────────────────────────
            // 前回起動日の 00:00:00 〜 翌日 00:00:00 を検索範囲とする UTC 文字列を生成する
            var prevBootDay    = latestBootTime.Value.Date;
            var dayStartUtc    = prevBootDay.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
            var dayEndUtc      = prevBootDay.AddDays(1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

            Debug.WriteLine($"[WindowsLoginService] 前回起動日: {prevBootDay:yyyy/MM/dd}（UTC範囲: {dayStartUtc} ～ {dayEndUtc}）");

            // ─── Step3 ──────────────────────────────────────────────────────────
            // 前回起動日の「最初の 起動（6005）」を取得する
            // ReverseDirection = false（昇順）で先頭1件 = その日の最初のログイン
            var firstBootQuery = new EventLogQuery("System", PathType.LogName,
                $"*[System[(EventID=6005) and TimeCreated[@SystemTime >= '{dayStartUtc}' and @SystemTime < '{dayEndUtc}']]]")
            {
                ReverseDirection = false   // 昇順（古い順）
            };

            DateTime? firstBootTime = null;
            using (var reader = new EventLogReader(firstBootQuery))
            {
                var rec = reader.ReadEvent();
                if (rec != null)
                    using (rec) firstBootTime = rec.TimeCreated?.ToLocalTime();
            }

            // ─── Step4 ──────────────────────────────────────────────────────────
            // 前回起動日の「最後の シャットダウン（6006）」を取得する
            // ReverseDirection = true（降順）で先頭1件 = その日の最後のシャットダウン
            var lastShutdownQuery = new EventLogQuery("System", PathType.LogName,
                $"*[System[(EventID=6006) and TimeCreated[@SystemTime >= '{dayStartUtc}' and @SystemTime < '{dayEndUtc}']]]")
            {
                ReverseDirection = true    // 降順（新しい順）
            };

            DateTime? lastShutdownTime = null;
            using (var reader = new EventLogReader(lastShutdownQuery))
            {
                var rec = reader.ReadEvent();
                if (rec != null)
                    using (rec) lastShutdownTime = rec.TimeCreated?.ToLocalTime();
            }

            // ─── Step5 ──────────────────────────────────────────────────────────
            // 6006 が取得できなかった場合、Event 1074 でフォールバックする
            // 1074 = ユーザー操作やプロセスによるシャットダウン/再起動要求
            // Windows 11 の高速スタートアップ有効環境では 6006 が記録されないことがあるため
            if (!lastShutdownTime.HasValue)
            {
                Debug.WriteLine("[WindowsLoginService] 6006 なし → 1074 でフォールバック");

                var fallbackShutdownQuery = new EventLogQuery("System", PathType.LogName,
                    $"*[System[(EventID=1074) and TimeCreated[@SystemTime >= '{dayStartUtc}' and @SystemTime < '{dayEndUtc}']]]")
                {
                    ReverseDirection = true
                };

                using var fbReader = new EventLogReader(fallbackShutdownQuery);
                var rec = fbReader.ReadEvent();
                if (rec != null)
                    using (rec) lastShutdownTime = rec.TimeCreated?.ToLocalTime();
            }

            Debug.WriteLine($"[WindowsLoginService] 結果 → 初回ログイン: {firstBootTime:HH:mm}, 最終ログアウト: {lastShutdownTime:HH:mm}");
            return (firstBootTime, lastShutdownTime);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsLoginService] ReadPreviousBootDayTimes エラー: {ex.Message}");
            return (null, null);
        }
    }
}
