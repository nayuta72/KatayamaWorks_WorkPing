namespace WorkPin.Models;

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
    /// コンボボックスの表示テキスト。
    /// ファイル名のみ（拡張子なし）を返す。パスが未設定の場合は空文字を返す。
    /// </summary>
    public override string ToString() =>
        string.IsNullOrWhiteSpace(FilePath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(FilePath);
}
