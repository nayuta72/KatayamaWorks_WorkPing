using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Xml.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using WorkPing.Features.AccountSettings.ViewModels;

namespace WorkPing.Features.AccountSettings.Views;

/// <summary>
/// アカウント設定ページのコードビハインド。
/// NavigationView の 3 ページ目として表示される。
/// </summary>
public sealed partial class AccountSettingsPage : Page
{
    /// <summary>
    /// このページにバインドされた ViewModel。
    /// XAML 側から x:Bind ViewModel.XXX として参照する。
    /// </summary>
    public AccountSettingsViewModel ViewModel { get; }

    public AccountSettingsPage()
    {
        InitializeComponent();

        // DI コンテナから ViewModel を取得する（遷移のたびに新しいインスタンスが生成される）
        ViewModel = App.ServiceProvider.GetRequiredService<AccountSettingsViewModel>();
    }

    // ===========================
    // 設定ファイルを開くボタン
    // ===========================

    /// <summary>
    /// settings.json を OS の既定アプリ（メモ帳など）で直接開く。
    /// ファイルが存在しない場合は何もしない。
    /// </summary>
    private void OpenSettingsFile_Click(object sender, RoutedEventArgs e)
    {
        // SettingsService と同じパス計算ロジック
        var appData    = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var filePath   = Path.Combine(appData, "kikakutools", "WorkPing", "settings.json");

        if (!File.Exists(filePath)) return;

        // UseShellExecute = true で既定アプリに開かせる（メモ帳など）
        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }

    // ===========================
    // スタートアップ登録ボタン
    // ===========================

    /// <summary>
    /// このツールの exe ショートカット (.lnk) を Windows のスタートアップフォルダに作成する。
    /// WScript.Shell COM オブジェクトを使用して .lnk を生成するため、
    /// 管理者権限は不要（ユーザースタートアップフォルダへの書き込み）。
    /// 既に登録済みの場合は上書きして最新の exe パスに更新する。
    /// </summary>
    private async void RegisterStartup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                throw new InvalidOperationException("実行ファイルのパスを取得できませんでした。");

            // ユーザーのスタートアップフォルダ
            // 例: %APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var linkPath      = Path.Combine(startupFolder, "WorkPing.lnk");

            // WScript.Shell COM オブジェクトでショートカットを作成する
            // .NET 標準ライブラリに lnk 生成 API がないため COM を使用する
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                            ?? throw new InvalidOperationException("WScript.Shell の取得に失敗しました。");
            dynamic shell    = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(linkPath);
            shortcut.TargetPath      = exePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
            shortcut.Description     = "WorkPing - 出退勤ツール";
            shortcut.Save();

            var dialog = new ContentDialog
            {
                Title           = "スタートアップ登録完了",
                Content         = $"WorkPing をスタートアップに登録しました。\n次回 Windows ログイン時から自動起動します。\n\n登録先：{linkPath}",
                CloseButtonText = "OK",
                XamlRoot        = XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var errorDialog = new ContentDialog
            {
                Title           = "スタートアップ登録エラー",
                Content         = $"スタートアップへの登録に失敗しました。\n{ex.Message}",
                CloseButtonText = "閉じる",
                XamlRoot        = XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    // ===========================
    // 新規ログファイル作成ボタン
    // ===========================

    private async void CreateLogFile0_Click(object sender, RoutedEventArgs e)
        => await PickAndCreateLogFileAsync(0);

    private async void CreateLogFile1_Click(object sender, RoutedEventArgs e)
        => await PickAndCreateLogFileAsync(1);

    private async void CreateLogFile2_Click(object sender, RoutedEventArgs e)
        => await PickAndCreateLogFileAsync(2);

    /// <summary>
    /// FileSavePicker でファイル保存先を選択させ、空の出退勤ログ XML を作成する。
    /// 作成後、対応するログパスの FilePath に保存先パスをセットする。
    /// </summary>
    /// <param name="index">LogFilePaths のインデックス（0〜2）</param>
    private async Task PickAndCreateLogFileAsync(int index)
    {
        // FileSavePicker は Win32 の HWND が必要なため、MainWindowHandle を渡す
        var picker = new FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        // ファイル名は "Log" + 年度（4月始まり）にする
        // 例：4〜12月なら当年、1〜3月なら前年が年度になる
        var now = DateTime.Now;
        var fiscalYear = now.Month >= 4 ? now.Year : now.Year - 1;
        picker.SuggestedFileName = $"Log{fiscalYear}";

        picker.FileTypeChoices.Add("XML ファイル", new List<string> { ".xml" });

        // ユーザーがファイル保存先を選択する（キャンセルした場合は null）
        StorageFile? file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            // 空の出退勤ログ XML（ルート要素のみ）を作成する
            // AttendanceLogService はこの形式を前提として読み書きする
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Root")
            );

            // ファイルに書き込む
            using var stream = await file.OpenStreamForWriteAsync();
            stream.SetLength(0); // 既存内容をクリア
            doc.Save(stream);

            // ViewModel のパスに選択したファイルのフルパスをセットする
            ViewModel.LogFilePaths[index].FilePath.Value = file.Path;
        }
        catch (Exception ex)
        {
            // エラーダイアログを表示する
            var errorDialog = new ContentDialog
            {
                Title = "ファイル作成エラー",
                Content = $"ファイルの作成に失敗しました。\n{ex.Message}",
                CloseButtonText = "閉じる",
                XamlRoot = XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }
}
