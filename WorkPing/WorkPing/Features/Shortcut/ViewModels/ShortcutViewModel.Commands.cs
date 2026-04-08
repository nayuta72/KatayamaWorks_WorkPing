using WorkPing.Models;

namespace WorkPing.Features.Shortcut.ViewModels;

/// <summary>
/// ショートカットページ ViewModel のロジック定義。
/// ショートカットの追加・更新・削除と settings.json への保存を担う。
/// </summary>
public partial class ShortcutViewModel
{
    // ===========================
    // ショートカット操作
    // ===========================

    /// <summary>
    /// 新しいショートカットをコレクションに追加して settings.json に保存する。
    /// </summary>
    /// <param name="name">ボタンに表示する名前</param>
    /// <param name="link">リンク先（ファイルパス・フォルダパス・URL）</param>
    public async Task AddShortcutAsync(string name, string link)
    {
        var item = new ShortcutItem
        {
            Name = name.Trim(),
            Link = link.Trim()
        };
        Shortcuts.Add(item);
        await SaveAsync();
    }

    /// <summary>
    /// 既存のショートカットの名前・リンク先を更新して settings.json に保存する。
    /// </summary>
    /// <param name="item">更新対象のショートカットアイテム</param>
    /// <param name="newName">新しい名前</param>
    /// <param name="newLink">新しいリンク先</param>
    public async Task UpdateShortcutAsync(ShortcutItem item, string newName, string newLink)
    {
        item.Name = newName.Trim();
        item.Link = newLink.Trim();
        await SaveAsync();
    }

    /// <summary>
    /// 指定したショートカットをコレクションから削除して settings.json に保存する。
    /// </summary>
    /// <param name="item">削除するショートカットアイテム</param>
    public async Task DeleteShortcutAsync(ShortcutItem item)
    {
        Shortcuts.Remove(item);
        await SaveAsync();
    }

    // ===========================
    // 設定保存（内部）
    // ===========================

    /// <summary>
    /// 現在のコレクション内容を settings.json に書き込む。
    /// </summary>
    private async Task SaveAsync()
    {
        _settingsService.Settings.Value.Shortcuts2 = Shortcuts.ToList();
        await _settingsService.SaveSettingsAsync();
    }
}
