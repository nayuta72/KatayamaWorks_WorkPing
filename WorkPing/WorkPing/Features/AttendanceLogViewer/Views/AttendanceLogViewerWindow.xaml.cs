using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WorkPing.Features.AttendanceLogViewer.ViewModels;

namespace WorkPing.Features.AttendanceLogViewer.Views;

/// <summary>
/// 勤怠ログ一覧を表示するモードレスウィンドウ。
///
/// MainWindow のタイトルバーにある SplitButton から開かれる。
/// 日付範囲（Today / Week / Month / All）と
/// 名前・日付テキストで絞り込みができる。
/// </summary>
public sealed partial class AttendanceLogViewerWindow : Window
{
    /// <summary>
    /// このウィンドウにバインドされた ViewModel。
    /// XAML 側から x:Bind ViewModel.XXX として参照する。
    /// </summary>
    public AttendanceLogViewerViewModel ViewModel { get; }

    // 現在読み込んでいるログファイルパス（再読み込みボタン用）
    private string _currentFilePath = string.Empty;

    public AttendanceLogViewerWindow()
    {
        InitializeComponent();

        // DI コンテナから ViewModel を取得する
        ViewModel = App.ServiceProvider.GetRequiredService<AttendanceLogViewerViewModel>();

        // コンテンツ領域をタイトルバーまで拡張し、ViewerTitleBar をドラッグ領域として登録する
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(ViewerTitleBar);

        // ウィンドウサイズを設定する
        SetWindowSize(900, 560);
    }

    // ===========================
    // 外部から呼ばれる操作メソッド
    // ===========================

    /// <summary>
    /// ログファイルを読み込み、指定の日付範囲で初期表示する。
    /// ウィンドウが表示される前後に MainWindow から呼び出す。
    /// </summary>
    /// <param name="filePath">読み込む XML ログファイルのパス</param>
    /// <param name="dateRange">初期日付範囲（"Today" / "Week" / "Month" / "All"）</param>
    public async Task LoadAsync(string filePath, string dateRange)
    {
        _currentFilePath = filePath;
        await ViewModel.LoadAsync(filePath, dateRange);
    }

    /// <summary>
    /// 日付範囲だけを変更して表示を更新する。
    /// タイトルバーのドロップダウンで範囲が切り替わったときに MainWindow から呼び出す。
    /// </summary>
    public void ChangeDateRange(string dateRange)
    {
        ViewModel.ChangeDateRange(dateRange);
    }

    // ===========================
    // イベントハンドラ
    // ===========================

    /// <summary>
    /// 再読み込みボタン：現在のファイルを再読み込みしてフィルタを再適用する。
    /// </summary>
    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath)) return;
        await ViewModel.LoadAsync(_currentFilePath, ViewModel.CurrentDateRange.Value);
    }

    // ===========================
    // ウィンドウサイズ設定
    // ===========================

    private void SetWindowSize(int width, int height)
    {
        var hwnd     = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWin   = AppWindow.GetFromWindowId(windowId);
        appWin.Resize(new SizeInt32(width, height));
    }
}
