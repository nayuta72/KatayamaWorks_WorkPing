using Reactive.Bindings.Extensions;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using WorkPing.Models;
using WorkPing.Services;

namespace WorkPing.Features.Shortcut.ViewModels;

/// <summary>
/// ショートカットページの ViewModel（基本定義・コンストラクタ・Dispose）。
/// partial クラスとして分割：
///   - ShortcutViewModel.cs (このファイル)：基本定義・コンストラクタ・設定読み込み
///   - ShortcutViewModel.Properties.cs：ReactiveProperty 定義
///   - ShortcutViewModel.Commands.cs：追加・更新・削除ロジック
/// </summary>
public partial class ShortcutViewModel : IDisposable
{
    // ===========================
    // 依存するサービス
    // ===========================
    private readonly SettingsService _settingsService;

    /// <summary>
    /// Dispose 対象をまとめた CompositeDisposable。
    /// すべての ReactiveProperty / ReactiveCommand を AddTo(Disposable) で登録する。
    /// </summary>
    public CompositeDisposable Disposable { get; } = new();

    public ShortcutViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeProperties();
        LoadFromSettings();
    }

    /// <summary>
    /// settings.json のショートカットリストをコレクションに読み込む。
    /// ページが表示されるたびに呼ばれる（ShortcutPage.OnNavigatedTo から呼ぶ）。
    /// </summary>
    public void LoadFromSettings()
    {
        Shortcuts.Clear();
        foreach (var item in _settingsService.Settings.Value.Shortcuts2)
            Shortcuts.Add(item);
    }

    public void Dispose()
    {
        Disposable.Dispose();
    }
}
