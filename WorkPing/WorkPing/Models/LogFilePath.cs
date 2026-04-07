using Microsoft.UI.Xaml;
using System.Text.Json.Serialization;

namespace WorkPing.Models;

/// <summary>
/// 出退勤ログファイルのパス情報を保持するモデル。
/// 部署を兼務するメンバーが複数のログファイルを管理するために使用する。
/// 最大 3 件まで登録可能。
/// </summary>
public class LogFilePath
{
    /// <summary>
    /// 部署ラベル（将来の拡張用に保持するが、現在は UI に表示しない）。
    /// </summary>
    public string DepartmentLabel { get; set; } = string.Empty;

    /// <summary>ログXMLファイルのフルパス（例：\\server\share\Log2026.xml）</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// コンボボックスの表示テキスト（ItemTemplate のバインディング用）。
    /// ファイル名のみ（拡張子なし）を返す。パスが未設定の場合は空文字を返す。
    /// </summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(FilePath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>ToString() は DisplayName と同じ値を返す（後方互換）。</summary>
    public override string ToString() => DisplayName;

    /// <summary>
    /// このファイルがメインのログファイル（書き込み先）かどうか。
    /// JSON には保存しない。コンボボックス生成時に MainLogFileIndex と照合して設定する。
    /// </summary>
    [JsonIgnore]
    public bool IsMain { get; set; }

    /// <summary>
    /// IsMain を Visibility に変換したプロパティ。コンボボックスのテンプレートで使用する。
    /// IsMain = true → Visible / false → Collapsed
    /// </summary>
    [JsonIgnore]
    public Visibility MainVisibility =>
        IsMain ? Visibility.Visible : Visibility.Collapsed;
}
