namespace WorkPing.Features.AttendanceLog.Models;

/// <summary>
/// 出退勤ログページのビジネスロジックを担うモデルクラス。
///
/// 主な役割：
/// - ステータスコードの変換（ボタン → XML に書き込む文字）
/// - 今日のログ状態が当日のものかどうかの判定
/// </summary>
public class AttendanceLogModel
{
    // ===========================
    // ステータスコード定数
    // ===========================

    /// <summary>出勤ステータス：体調良好（笑顔ボタン）</summary>
    public const string StatusGood   = "◯";

    /// <summary>出勤ステータス：いつもどおり（真顔ボタン）</summary>
    public const string StatusNormal = "△";

    /// <summary>出勤ステータス：体調不良 / 負荷高（辛そうな顔ボタン）</summary>
    public const string StatusBad    = "✕";

    /// <summary>勤務形態：出社</summary>
    public const string WorkTypeOffice = "出社";

    /// <summary>勤務形態：在宅</summary>
    public const string WorkTypeRemote = "在宅";

    // ===========================
    // ヘルパーメソッド
    // ===========================

    /// <summary>
    /// 今日の日付文字列（yyyyMMdd 形式）を返す。
    /// XML エレメント名の生成に使用する。
    /// </summary>
    public string GetTodayDateString() => DateTime.Today.ToString("yyyyMMdd");

    /// <summary>
    /// 保存されている日付が今日のものかどうかを確認する。
    /// ツール再起動後にステータスを復元するか否かの判定に使用する。
    /// </summary>
    /// <param name="savedDate">settings.json に保存されている日付（yyyyMMdd 形式）</param>
    public bool IsTodayLog(string? savedDate) =>
        savedDate == GetTodayDateString();

    /// <summary>
    /// ステータス文字から対応する表示色名を返す。
    /// ViewModel でボタンの色を変えるために使用する。
    /// </summary>
    /// <param name="status">◯/△/✕</param>
    /// <returns>色の名前（"Green" / "Yellow" / "Red" / null）</returns>
    public static string? GetStatusColorName(string? status) => status switch
    {
        StatusGood   => "Green",
        StatusNormal => "Yellow",
        StatusBad    => "Red",
        _            => null
    };
}
