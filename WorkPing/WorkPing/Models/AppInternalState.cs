namespace WorkPing.Models;

/// <summary>
/// アプリが内部的に管理する状態データ。
/// ユーザーが直接編集することを想定しない自動保存データをまとめたクラス。
/// settings.json 内では "InternalState" キーの下にネストして保存される。
/// </summary>
public class AppInternalState
{
    // ===========================
    // ログファイル選択
    // ===========================

    /// <summary>
    /// メインのログファイルパスのインデックス（0 始まり）。
    /// 自分の出退勤ログの書き込み先として使用する。
    /// アカウント設定画面で設定し、コンボボックスの操作では変わらない。
    /// </summary>
    public int MainLogFileIndex { get; set; } = 0;

    // ===========================
    // 最終ログ日付
    // ===========================

    /// <summary>
    /// 最後に出退勤を記録した日付（yyyyMMdd 形式）。
    /// 日付が変わったときに Today 系データをリセットする判定に使用する。
    /// </summary>
    public string? LastLogDate { get; set; }

    // ===========================
    // 当日の出退勤データ（日付変更時にリセットされる）
    // ===========================

    /// <summary>今日の出勤時刻（HH:mm 形式）。再起動後にボタン色を復元するために保存する。</summary>
    public string? TodayClockInTime { get; set; }

    /// <summary>今日の出勤ステータス（◯/△/✕）。</summary>
    public string? TodayClockInStatus { get; set; }

    /// <summary>今日の退勤時刻（HH:mm 形式）。</summary>
    public string? TodayClockOutTime { get; set; }

    /// <summary>今日の退勤ステータス（◯/△/✕）。</summary>
    public string? TodayClockOutStatus { get; set; }

    /// <summary>今日の勤務形態（"出社" / "在宅"）。</summary>
    public string? TodayWorkType { get; set; }

    /// <summary>
    /// XML ログファイルへの書き込みが保留中かどうかを示すフラグ。
    /// true の場合、次回ファイルアクセス可能時に settings の内容で再書き込みを試みる。
    /// アプリを終了・再起動しても書き込み漏れが発生しないように永続化している。
    /// </summary>
    public bool HasPendingWrite { get; set; } = false;

    // ===========================
    // リセット
    // ===========================

    /// <summary>
    /// 当日分のデータ（Today 系フィールドと HasPendingWrite）をリセットする。
    /// LastLogDate と MainLogFileIndex はリセット対象外。
    /// ツール起動時に日付が変わっていた場合に SettingsService から呼ばれる。
    /// </summary>
    public void ResetDailyData()
    {
        TodayClockInTime   = null;
        TodayClockInStatus = null;
        TodayClockOutTime  = null;
        TodayClockOutStatus = null;
        TodayWorkType      = null;
        HasPendingWrite    = false;
    }
}
