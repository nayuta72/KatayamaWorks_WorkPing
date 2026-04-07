using System.Text.Json.Serialization;

namespace WorkPing.Models;

/// <summary>
/// アプリケーションの設定情報を保持するモデルクラス。
/// settings.json への JSON シリアライズ・デシリアライズに使用する。
/// 保存先：%AppData%\Roaming\kikakutools\WorkPing\settings.json
/// </summary>
public class AppSettings
{
    // ===========================
    // ユーザー情報
    // ===========================

    /// <summary>姓（例：山田）</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>名（例：太郎）</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>部署名</summary>
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>管理者フラグ（true の場合、ファイル変更通知機能が有効になる）</summary>
    public bool IsAdmin { get; set; } = false;

    // ===========================
    // ログファイルパス
    // ===========================

    /// <summary>
    /// 出退勤ログファイルパスのリスト（最大3件）。
    /// 部署を兼務する人が複数のログファイルを切り替えて使うための設定。
    /// </summary>
    public List<LogFilePath> LogFilePaths { get; set; } = new();

    // ===========================
    // ショートカット
    // ===========================

    /// <summary>
    /// ショートカットボタンのリスト。
    /// ファイルパス・フォルダパス・URL を登録でき、上限なしに追加可能。
    /// </summary>
    public List<ShortcutItem> Shortcuts { get; set; } = new();

    // ===========================
    // 内部状態データ
    // ===========================

    /// <summary>
    /// アプリが内部的に管理する状態データ。
    /// ユーザーが直接編集することを想定しない自動保存データをまとめたオブジェクト。
    /// （コンボボックス選択位置、当日の出退勤時刻・ステータス、保留フラグなど）
    /// </summary>
    public AppInternalState InternalState { get; set; } = new();

    // ===========================
    // 計算プロパティ（JSON 非保存）
    // ===========================

    /// <summary>フルネーム（姓 + 名）を返す。JSONには保存しない。</summary>
    [JsonIgnore]
    public string FullName => $"{LastName} {FirstName}".Trim();

    /// <summary>現在有効なログファイルパス情報を返す。JSONには保存しない。</summary>
    [JsonIgnore]
    public LogFilePath? CurrentLogFilePath =>
        LogFilePaths.Count > InternalState.DefaultLogFileIndex
            ? LogFilePaths[InternalState.DefaultLogFileIndex]
            : LogFilePaths.Count > 0
                ? LogFilePaths[0]
                : null;
}
