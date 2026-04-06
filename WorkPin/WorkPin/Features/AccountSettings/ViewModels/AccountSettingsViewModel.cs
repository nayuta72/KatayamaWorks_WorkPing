using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Reactive.Disposables;
using WorkPin.Features.AccountSettings.Models;
using WorkPin.Services;

namespace WorkPin.Features.AccountSettings.ViewModels;

/// <summary>
/// アカウント設定ページの ViewModel（基本定義・コンストラクタ・Dispose）。
/// partial クラスとして分割：
///   - AccountSettingsViewModel.cs (このファイル)：基本定義・コンストラクタ
///   - AccountSettingsViewModel.Properties.cs：ReactiveProperty 定義
///   - AccountSettingsViewModel.Commands.cs：ReactiveCommand とロジック
/// </summary>
public partial class AccountSettingsViewModel : IDisposable
{
    // ===========================
    // 依存するサービスとモデル
    // ===========================
    private readonly SettingsService _settingsService;
    private readonly AccountSettingsModel _model;

    /// <summary>
    /// Dispose 対象をまとめた CompositeDisposable。
    /// すべての ReactiveProperty / ReactiveCommand を AddTo(Disposable) で登録する。
    /// </summary>
    public CompositeDisposable Disposable { get; } = new();

    public AccountSettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _model           = new AccountSettingsModel();

        // プロパティとコマンドを初期化する（各 partial ファイルの Initialize メソッドを呼ぶ）
        InitializeProperties();
        InitializeCommands();

        // 現在の設定値を画面に反映する
        LoadFromSettings();
    }

    /// <summary>
    /// settings.json から現在の値を各プロパティに読み込む。
    /// </summary>
    private void LoadFromSettings()
    {
        var s = _settingsService.Settings.Value;

        LastName.Value       = s.LastName;
        FirstName.Value      = s.FirstName;
        UserId.Value         = s.UserId;
        DepartmentName.Value = s.DepartmentName;
        IsAdmin.Value        = s.IsAdmin;

        // ログファイルパスを最大 3 件分読み込む（不足分は空で埋める）
        for (var i = 0; i < 3; i++)
        {
            LogFilePaths[i].FilePath.Value = i < s.LogFilePaths.Count
                ? s.LogFilePaths[i].FilePath
                : string.Empty;
        }

        DefaultLogFileIndex.Value = s.InternalState.DefaultLogFileIndex;
    }

    public void Dispose()
    {
        Disposable.Dispose();
    }
}
