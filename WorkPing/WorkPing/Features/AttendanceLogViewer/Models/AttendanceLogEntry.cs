using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WorkPing.Features.AttendanceLogViewer.Models;

/// <summary>
/// 勤怠ログ一覧ビューアーで表示するログエントリーの表示用モデル。
/// XML から読み込んだデータをビューに適した形式へ変換して保持する。
/// </summary>
public class AttendanceLogEntry
{
    /// <summary>日付（フィルタリングと日付範囲の判定に使用）</summary>
    public DateTime Date { get; set; }

    /// <summary>表示用日付文字列（yyyy/MM/dd 形式）</summary>
    public string DateDisplay => Date.ToString("yyyy/MM/dd");

    /// <summary>氏名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>勤務形態（出社 / 在宅）</summary>
    public string WorkType { get; set; } = string.Empty;

    /// <summary>出勤時刻（HH:mm 形式）</summary>
    public string ClockIn { get; set; } = string.Empty;

    /// <summary>出勤ステータス（◯ = 体調良好 / △ = いつもどおり / ✕ = 体調不良）</summary>
    public string ClockInStatus { get; set; } = string.Empty;

    /// <summary>退勤時刻（HH:mm 形式）</summary>
    public string ClockOut { get; set; } = string.Empty;

    /// <summary>退勤ステータス（◯ = 順調 / △ = 計画どおり / ✕ = 負荷高）</summary>
    public string ClockOutStatus { get; set; } = string.Empty;

    /// <summary>コメント</summary>
    public string Comment { get; set; } = string.Empty;

    // ===========================
    // 勤務形態表示用プロパティ
    // ===========================

    /// <summary>
    /// 在宅勤務の場合のみ HOME アイコンを表示するための Visibility。
    /// 出社（または未設定）の場合は Collapsed にして空欄表示にする。
    /// </summary>
    public Visibility RemoteIconVisibility =>
        WorkType == "在宅" ? Visibility.Visible : Visibility.Collapsed;

    // ===========================
    // ステータス表示用プロパティ
    // ===========================

    /// <summary>
    /// 出勤ステータス（体調）を顔絵文字に変換して返す。
    /// ◯→😊 △→😐 ✕→😞 未記入→空文字
    /// </summary>
    public string ClockInStatusEmoji  => StatusToEmoji(ClockInStatus);

    /// <summary>
    /// 退勤ステータス（負荷）を顔絵文字に変換して返す。
    /// ◯→😊 △→😐 ✕→😞 未記入→空文字
    /// </summary>
    public string ClockOutStatusEmoji => StatusToEmoji(ClockOutStatus);

    /// <summary>
    /// 出勤ステータス（体調）に対応する背景ブラシを返す。
    /// ◯→パステルグリーン △→パステルイエロー ✕→パステルレッド 未記入→透明
    /// </summary>
    public Brush ClockInStatusBackground  => StatusToBrush(ClockInStatus);

    /// <summary>
    /// 退勤ステータス（負荷）に対応する背景ブラシを返す。
    /// ◯→パステルグリーン △→パステルイエロー ✕→パステルレッド 未記入→透明
    /// </summary>
    public Brush ClockOutStatusBackground => StatusToBrush(ClockOutStatus);

    // ===========================
    // プライベートヘルパー
    // ===========================

    /// <summary>ステータス文字を顔絵文字に変換する。</summary>
    private static string StatusToEmoji(string status) => status switch
    {
        "◯" => "😊",
        "△" => "😐",
        "✕" => "😞",
        _   => string.Empty
    };

    /// <summary>
    /// ステータス文字を背景用の半透明パステルブラシに変換する。
    /// 1ページ目のステータスボタンと同じパステルカラー（alpha=96 ≒ 38%透過）を使用する。
    /// </summary>
    private static Brush StatusToBrush(string status) => status switch
    {
        "◯" => new SolidColorBrush(Color.FromArgb(160, 127, 191, 127)),  // パステルグリーン
        "△" => new SolidColorBrush(Color.FromArgb(160, 236, 210, 143)),  // パステルイエロー
        "✕" => new SolidColorBrush(Color.FromArgb(160, 237, 137, 157)),  // パステルレッド
        _   => new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))            // 透明
    };
}
