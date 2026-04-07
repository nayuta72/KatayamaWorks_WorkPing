using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Reactive.Disposables;
using WorkPing.Models;

namespace WorkPing.Features.AccountSettings.ViewModels;

/// <summary>
/// アカウント設定 ViewModel の ReactiveProperty 定義。
/// バリデーション不要なため ReactivePropertySlim を使用する（パフォーマンス優先）。
/// </summary>
public partial class AccountSettingsViewModel
{
    // ===========================
    // ユーザー情報
    // ===========================

    /// <summary>姓の入力値</summary>
    public ReactivePropertySlim<string> LastName { get; private set; } = null!;

    /// <summary>名の入力値</summary>
    public ReactivePropertySlim<string> FirstName { get; private set; } = null!;

    /// <summary>部署名の入力値</summary>
    public ReactivePropertySlim<string> DepartmentName { get; private set; } = null!;

    /// <summary>管理者フラグ（true = 管理者）</summary>
    public ReactivePropertySlim<bool> IsAdmin { get; private set; } = null!;

    // ===========================
    // ログファイルパス（最大3件）
    // ===========================

    /// <summary>
    /// ログファイルパスのリスト（UI バインド用の ViewModel）。
    /// 3 件固定で用意し、空欄は無視して保存する。
    /// </summary>
    public List<LogFilePathViewModel> LogFilePaths { get; private set; } = null!;

    /// <summary>デフォルトで使用するログファイルのインデックス（0〜2）</summary>
    public ReactivePropertySlim<int> DefaultLogFileIndex { get; private set; } = null!;

    // ===========================
    // 状態プロパティ
    // ===========================

    /// <summary>保存処理中かどうかを示すフラグ（ボタンの無効化に使用）</summary>
    public ReactivePropertySlim<bool> IsSaving { get; private set; } = null!;

    /// <summary>
    /// プロパティを初期化する（コンストラクタから呼ばれる）。
    /// </summary>
    private void InitializeProperties()
    {
        LastName             = new ReactivePropertySlim<string>(string.Empty).AddTo(Disposable);
        FirstName            = new ReactivePropertySlim<string>(string.Empty).AddTo(Disposable);
        DepartmentName       = new ReactivePropertySlim<string>(string.Empty).AddTo(Disposable);
        IsAdmin              = new ReactivePropertySlim<bool>(false).AddTo(Disposable);
        DefaultLogFileIndex  = new ReactivePropertySlim<int>(0).AddTo(Disposable);
        IsSaving             = new ReactivePropertySlim<bool>(false).AddTo(Disposable);

        // ログファイルパス ViewModel を 3 件初期化する
        LogFilePaths = Enumerable.Range(0, 3)
            .Select(_ => new LogFilePathViewModel(Disposable))
            .ToList();
    }
}

/// <summary>
/// ログファイルパス 1 件分の入力欄に対応する ViewModel。
/// アカウント設定ページで 3 行分使用する。
/// </summary>
public class LogFilePathViewModel
{
    /// <summary>ファイルパスの入力値</summary>
    public ReactivePropertySlim<string> FilePath { get; }

    public LogFilePathViewModel(CompositeDisposable disposable)
    {
        FilePath = new ReactivePropertySlim<string>(string.Empty).AddTo(disposable);
    }

    /// <summary>このエントリーの内容を WorkPing.Models.LogFilePath に変換して返す。</summary>
    public LogFilePath ToModel() => new()
    {
        FilePath = FilePath.Value
    };
}
