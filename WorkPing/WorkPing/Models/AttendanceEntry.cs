namespace WorkPing.Models;

/// <summary>
/// 出退勤ログの書き込み操作の種別。
/// AttendanceLogService の UpdateLastLog でどの属性を書き込むかの判定に使う。
/// </summary>
public enum AttendanceAction
{
    /// <summary>出勤ステータスボタンを押した</summary>
    ClockIn,
    /// <summary>退勤ステータスボタンを押した</summary>
    ClockOut,
    /// <summary>コメント送信ボタンを押した</summary>
    Comment
}

/// <summary>
/// 出退勤ログのエントリーを表すモデル。
/// XMLファイルへの書き込み内容をまとめたクラス。
/// XML エレメント名は "log" 固定で、Date 属性と Name 属性で一意のエントリーを識別する。
/// </summary>
public class AttendanceEntry
{
    /// <summary>日付（yyyyMMdd 形式、例：20260404）。Date 属性に書き込む。</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>氏名（Name 属性に書き込む）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>勤務形態（"出社" または "在宅"）</summary>
    public string WorkType { get; set; } = string.Empty;

    /// <summary>出勤時刻（HH:mm 形式）。null の場合は属性を更新しない。</summary>
    public string? ClockInTime { get; set; }

    /// <summary>出勤ステータス（◯ = 体調良好 / △ = いつもどおり / ✕ = 体調不良）</summary>
    public string? ClockInStatus { get; set; }

    /// <summary>退勤時刻（HH:mm 形式）。null の場合は属性を更新しない。</summary>
    public string? ClockOutTime { get; set; }

    /// <summary>退勤ステータス（◯ = 順調 / △ = 計画どおり / ✕ = 負荷高）</summary>
    public string? ClockOutStatus { get; set; }

    /// <summary>コメント（最大20文字）</summary>
    public string? Comment { get; set; }

    /// <summary>
    /// この書き込みを引き起こした操作の種別。
    /// UpdateLastLog でどの属性だけを書き込むかの判定に使用する。
    /// </summary>
    public AttendanceAction Action { get; set; } = AttendanceAction.ClockIn;
}
