namespace WorkPin.Models;

/// <summary>
/// ショートカット1件分のデータモデル。
/// ファイルパス・フォルダパス・URL のいずれかを登録できる。
/// settings.json の "Shortcuts" 配列の要素として JSON シリアライズされる。
/// </summary>
public class ShortcutItem
{
    /// <summary>ボタンに表示する名前（例：「共有フォルダ」「社内ポータル」）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// リンク先。以下のいずれかを設定する。
    ///   - ファイルパス  : C:\Users\...\document.xlsx
    ///   - フォルダパス  : \\server\share\folder
    ///   - URL           : https://example.com
    /// </summary>
    public string Link { get; set; } = string.Empty;
}
