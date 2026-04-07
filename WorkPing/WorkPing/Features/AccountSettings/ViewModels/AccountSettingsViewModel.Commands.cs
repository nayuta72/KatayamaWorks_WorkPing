using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Reactive.Linq;
using WorkPing.Models;

namespace WorkPing.Features.AccountSettings.ViewModels;

/// <summary>
/// アカウント設定 ViewModel のコマンド定義とロジック。
/// </summary>
public partial class AccountSettingsViewModel
{
    // ===========================
    // コマンド定義
    // ===========================

    /// <summary>
    /// 設定を保存するコマンド。
    /// 保存中（IsSaving = true）のときは実行できない。
    /// </summary>
    public ReactiveCommandSlim SaveCommand { get; private set; } = null!;

    /// <summary>コマンドを初期化する（コンストラクタから呼ばれる）。</summary>
    private void InitializeCommands()
    {
        // IsSaving が false のときのみ実行可能にする
        SaveCommand = IsSaving
            .Select(isSaving => !isSaving)
            .ToReactiveCommandSlim()
            .AddTo(Disposable);

        SaveCommand.Subscribe(async _ => await SaveAsync());
    }

    // ===========================
    // コマンドの実装ロジック
    // ===========================

    /// <summary>
    /// 画面の入力内容を settings.json に保存する。
    /// 空のログパスは除外して保存する。
    /// </summary>
    // View（AccountSettingsDialog）からも直接 await できるよう public にする
    public async Task SaveAsync()
    {
        IsSaving.Value = true;

        try
        {
            // 入力値を AppSettings モデルに反映する
            var settings = _settingsService.Settings.Value;

            settings.LastName       = LastName.Value.Trim();
            settings.FirstName      = FirstName.Value.Trim();
            settings.UserId         = UserId.Value.Trim();
            settings.DepartmentName = DepartmentName.Value.Trim();
            settings.IsAdmin        = IsAdmin.Value;

            // 空でないログパスのみを保存する（最大3件）
            settings.LogFilePaths = _model.FilterValidPaths(
                LogFilePaths.Select(lp => lp.ToModel())
            );

            // デフォルトインデックスが有効範囲内かを確認する
            settings.InternalState.DefaultLogFileIndex = settings.LogFilePaths.Count > 0
                ? Math.Clamp(DefaultLogFileIndex.Value, 0, settings.LogFilePaths.Count - 1)
                : 0;

            // settings.json に書き込む
            await _settingsService.SaveSettingsAsync();

            System.Diagnostics.Debug.WriteLine("[AccountSettingsViewModel] 設定を保存しました。");
        }
        finally
        {
            IsSaving.Value = false;
        }
    }
}
