using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Xml.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using WorkPin.Features.AccountSettings.ViewModels;

namespace WorkPin.Features.AccountSettings.Views;

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
    // 設定フォルダを開くボタン
    // ===========================

    /// <summary>
    /// settings.json が格納されているフォルダ（%AppData%\Roaming\kikakutools\WorkPin）を
    /// エクスプローラーで開く。フォルダが存在しない場合は何もしない。
    /// </summary>
    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
    {
        // SettingsService と同じパス計算ロジック
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folderPath = Path.Combine(appData, "kikakutools", "WorkPin");

        if (!Directory.Exists(folderPath)) return;

        // explorer.exe でフォルダを開く
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"")
        {
            UseShellExecute = true
        });
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
