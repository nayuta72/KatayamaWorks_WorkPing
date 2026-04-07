using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WorkPing.Features.AttendanceLog.ViewModels;

namespace WorkPing.Features.AttendanceLog.Views;

/// <summary>
/// 出退勤ログページのコードビハインド。
/// ViewModel のイベントを購読してダイアログの表示などの View 層の処理を担う。
/// </summary>
public sealed partial class AttendanceLogPage : Page
{
    /// <summary>
    /// このページにバインドされた ViewModel。
    /// XAML 側から x:Bind ViewModel.XXX として参照する。
    /// </summary>
    public AttendanceLogViewModel ViewModel { get; }

    public AttendanceLogPage()
    {
        InitializeComponent();

        // DI コンテナから ViewModel を取得する
        ViewModel = App.ServiceProvider.GetRequiredService<AttendanceLogViewModel>();

        // コメントダイアログ表示のイベントを購読する
        // （ViewModel からダイアログ表示をリクエストされたときに View 側で ContentDialog を表示する）
        ViewModel.ShowCommentDialogRequested += ShowCommentDialogAsync;
    }

    /// <summary>
    /// ページが表示された直後に呼ばれる。
    /// Windows ログイン時刻の取得など非同期初期化を実行する。
    /// </summary>
    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        App.Trace("AttendanceLogPage Loaded start");
        try
        {
            await ViewModel.InitializeAsync();
            App.Trace("AttendanceLogPage InitializeAsync OK");
        }
        catch (Exception ex)
        {
            App.Trace($"AttendanceLogPage Page_Loaded EXCEPTION: {ex}");
        }
    }

    // ===========================
    // コメントダイアログ
    // ===========================

    /// <summary>
    /// コメント入力 ContentDialog を表示し、入力されたコメントを返す。
    /// キャンセルされた場合は null を返す。
    /// ViewModel の ShowCommentDialogRequested イベントから呼ばれる。
    /// </summary>
    private async Task<string?> ShowCommentDialogAsync()
    {
        var textBox = new TextBox
        {
            PlaceholderText = "コメントを入力（最大20文字）",
            MaxLength = 20,
            Width = 240
        };

        var dialog = new ContentDialog
        {
            Title              = "コメントを入力",
            Content            = textBox,
            PrimaryButtonText  = "送信",
            CloseButtonText    = "キャンセル",
            DefaultButton      = ContentDialogButton.Primary,
            XamlRoot           = XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return textBox.Text;
        }

        return null; // キャンセル
    }

    // ===========================
    // 外部サイトへのジャンプボタン
    // ===========================

    /// <summary>就業管理システムを既定ブラウザで開く。</summary>
    private void WorktimeSystemButton_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://www.yahoo.co.jp/");

    /// <summary>M-Plus を既定ブラウザで開く。</summary>
    private void MPlusButton_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://maps.google.com");

    /// <summary>生産管理システムを既定ブラウザで開く。</summary>
    private void ProductionSystemButton_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://www.google.com/?pli=1");

    /// <summary>
    /// 指定 URL を OS の既定ブラウザで開く。
    /// UseShellExecute = true を指定することで URL をブラウザに渡せる。
    /// </summary>
    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.Trace($"[AttendanceLogPage] URL 起動エラー: {url} / {ex.Message}");
        }
    }

    // ===========================
    // ボタン色変換メソッド（x:Bind で使用）
    // ===========================
    // WinUI 3 の x:Bind は関数バインドをサポートしているため、
    // ステータスが選択中かどうかに応じてブラシを返す。

    public Brush GetClockInGoodColor(bool isSelected) =>
        isSelected ? new SolidColorBrush(ColorHelper.FromArgb(255, 127, 191, 127)) : GetDefaultButtonBrush();

    public Brush GetClockInNormalColor(bool isSelected) =>
        isSelected ? new SolidColorBrush(ColorHelper.FromArgb(255, 236, 210, 143)) : GetDefaultButtonBrush();

    public Brush GetClockInBadColor(bool isSelected) =>
        isSelected ? new SolidColorBrush(ColorHelper.FromArgb(255, 237, 137, 157)) : GetDefaultButtonBrush();

    public Brush GetClockOutGoodColor(bool isSelected) =>
        isSelected ? new SolidColorBrush(ColorHelper.FromArgb(255, 127, 191, 127)) : GetDefaultButtonBrush();

    public Brush GetClockOutNormalColor(bool isSelected) =>
        isSelected ? new SolidColorBrush(ColorHelper.FromArgb(255, 236, 210, 143)) : GetDefaultButtonBrush();

    public Brush GetClockOutBadColor(bool isSelected) =>
        isSelected ? new SolidColorBrush(ColorHelper.FromArgb(255, 237, 137, 157)) : GetDefaultButtonBrush();

    // 未選択時のデフォルトボタン背景色
    // テーマリソース "ButtonBackground" が存在しない場合に備えて安全に取得する
    private static Brush GetDefaultButtonBrush()
    {
        if (Application.Current.Resources.TryGetValue("ButtonBackground", out var value) && value is Brush brush)
            return brush;
        // フォールバック：透明（ボタンのデフォルト外観を壊さない）
        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
}
