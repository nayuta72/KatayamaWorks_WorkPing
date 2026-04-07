using System.Collections.ObjectModel;
using WorkPing.Models;

namespace WorkPing.Features.Shortcut.ViewModels;

/// <summary>
/// ショートカットページ ViewModel のプロパティ定義。
/// </summary>
public partial class ShortcutViewModel
{
    // ===========================
    // ショートカットコレクション
    // ===========================

    /// <summary>
    /// 登録済みショートカットのコレクション。
    /// ShortcutPage がこれを監視して動的にボタンを生成する。
    /// </summary>
    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();

    /// <summary>プロパティを初期化する（コンストラクタから呼ばれる）。</summary>
    private void InitializeProperties()
    {
        // Shortcuts は宣言と同時に初期化しているため、ここでの処理は不要
    }
}
