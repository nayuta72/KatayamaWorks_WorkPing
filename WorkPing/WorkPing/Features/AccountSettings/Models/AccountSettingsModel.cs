using WorkPing.Models;

namespace WorkPing.Features.AccountSettings.Models;

/// <summary>
/// アカウント設定画面のビジネスロジックを担うモデルクラス。
/// ViewModel から呼ばれ、設定の検証・保存を行う。
/// </summary>
public class AccountSettingsModel
{
    /// <summary>
    /// ログファイルパスの最大登録件数（仕様：3件まで）。
    /// </summary>
    public const int MaxLogFilePaths = 3;

    /// <summary>
    /// 入力されたログファイルパスが有効かどうかを確認する。
    /// 現時点ではフォーマットチェックのみを行う。
    /// （ファイルの実存在チェックは行わない：新規ファイルも許可するため）
    /// </summary>
    /// <param name="path">確認するファイルパス</param>
    /// <returns>有効なパスの場合 true</returns>
    public bool IsValidFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            // Path.GetFullPath で無効なパス文字が含まれていないか確認する
            _ = Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 空でないログファイルパスのリストを返す（最大3件に絞る）。
    /// </summary>
    public List<LogFilePath> FilterValidPaths(IEnumerable<LogFilePath> paths)
    {
        return paths
            .Where(p => !string.IsNullOrWhiteSpace(p.FilePath))
            .Take(MaxLogFilePaths)
            .ToList();
    }
}
