using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WorkPing.Features.AttendanceLog.ViewModels;
using WorkPing.Models;

namespace WorkPing.Features.AttendanceLog.Views;

/// <summary>
/// 出退勤ログページのコードビハインド。
/// ViewModel のイベントを購読してダイアログの表示などの View 層の処理を担う。
/// ショートカットボタンは Shortcuts1 から動的に生成する。
/// </summary>
public sealed partial class AttendanceLogPage : Page
{
    // ショートカットボタン1個のサイズ
    private const double ShortcutButtonWidth  = 180;
    private const double ShortcutButtonHeight = 40;
    private const double ShortcutButtonMargin = 4;
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
    /// Windows ログイン時刻の取得など非同期初期化と、ショートカットボタンの構築を実行する。
    /// </summary>
    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        App.Trace("AttendanceLogPage Loaded start");
        try
        {
            await ViewModel.InitializeAsync();
            App.Trace("AttendanceLogPage InitializeAsync OK");

            // settings.json から Shortcuts1 を読み込んでボタンを構築する
            ViewModel.LoadShortcuts1FromSettings();
            BuildShortcut1Buttons();
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
    // ショートカットボタン動的生成（Shortcuts1）
    // ===========================

    /// <summary>
    /// AttendanceShortcutPanel のショートカットボタンを再構築する。
    /// ボタンは横一列に並び、「追加」ボタン（最後の1個）を残しつつ
    /// それより前の全ボタンを削除してから ViewModel.AttendanceShortcuts の順で挿入する。
    /// 「追加」ボタンは常に一番右に表示される。
    /// </summary>
    private void BuildShortcut1Buttons()
    {
        // 「追加」ボタン（最後の子）を残し、それ以外をすべて削除する
        while (AttendanceShortcutPanel.Children.Count > 1)
            AttendanceShortcutPanel.Children.RemoveAt(0);

        // ショートカットボタンを左から順に「追加」ボタンの前に挿入する
        int insertIndex = 0;
        foreach (var item in ViewModel.AttendanceShortcuts)
        {
            AttendanceShortcutPanel.Children.Insert(insertIndex, CreateShortcut1Button(item));
            insertIndex++;
        }
    }

    /// <summary>
    /// ショートカットアイテム1件分のボタンを生成する。
    ///   - クリック → リンク先を起動
    ///   - 右クリック → 編集 / 削除 メニュー
    /// </summary>
    private Button CreateShortcut1Button(ShortcutItem item)
    {
        var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        namePanel.Children.Add(new FontIcon { Glyph = GetLinkGlyph(item.Link), FontSize = 14 });
        namePanel.Children.Add(new TextBlock
        {
            Text         = item.Name,
            FontWeight   = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth     = ShortcutButtonWidth - 40,
            VerticalAlignment = VerticalAlignment.Center
        });

        var content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Left };
        content.Children.Add(namePanel);

        var button = new Button
        {
            Width    = ShortcutButtonWidth,
            Height   = ShortcutButtonHeight,
            Margin   = new Thickness(ShortcutButtonMargin),
            Content  = content,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        button.Click += (_, _) => LaunchLink(item.Link);

        // 右クリックメニュー
        var flyout   = new MenuFlyout();
        var editItem = new MenuFlyoutItem { Text = "編集" };
        var delItem  = new MenuFlyoutItem { Text = "削除" };
        editItem.Click += async (_, _) => await EditShortcut1Async(item);
        delItem.Click  += async (_, _) => await DeleteShortcut1Async(item);
        flyout.Items.Add(editItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(delItem);
        button.ContextFlyout = flyout;

        return button;
    }

    // ===========================
    // ショートカット追加・編集・削除（Shortcuts1）
    // ===========================

    private async void AddShortcut1Button_Click(object sender, RoutedEventArgs e)
    {
        var result = await ShowShortcut1DialogAsync("ショートカットを追加");
        if (result == null) return;
        await ViewModel.AddShortcut1Async(result.Value.Name, result.Value.Link);
        BuildShortcut1Buttons();
    }

    private async Task EditShortcut1Async(ShortcutItem item)
    {
        var result = await ShowShortcut1DialogAsync("ショートカットを編集", item.Name, item.Link);
        if (result == null) return;
        await ViewModel.UpdateShortcut1Async(item, result.Value.Name, result.Value.Link);
        BuildShortcut1Buttons();
    }

    private async Task DeleteShortcut1Async(ShortcutItem item)
    {
        var confirmDialog = new ContentDialog
        {
            Title             = "削除の確認",
            Content           = $"「{item.Name}」を削除しますか？",
            PrimaryButtonText = "削除",
            CloseButtonText   = "キャンセル",
            DefaultButton     = ContentDialogButton.Close,
            XamlRoot          = XamlRoot
        };
        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary) return;
        await ViewModel.DeleteShortcut1Async(item);
        BuildShortcut1Buttons();
    }

    /// <summary>
    /// 名前とリンク先を入力する ContentDialog を表示する（追加・編集共用）。
    /// キャンセルまたは必須項目が空の場合は null を返す。
    /// </summary>
    private async Task<(string Name, string Link)?> ShowShortcut1DialogAsync(
        string dialogTitle,
        string currentName = "",
        string currentLink = "")
    {
        // リンク先入力欄
        var linkRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        linkRow.Children.Add(new TextBlock { Text = "リンク先", Width = 50 });
        var linkBox = new TextBox
        {
            PlaceholderText = "\\\\server\\share\\folder  /  C:\\Users\\...  /  https://...",
            Text = currentLink
        };
        linkRow.Children.Add(linkBox);

        // 参照ボタン
        var fileButton   = new Button { Content = "📄 ファイル", Margin = new Thickness(0, 0, 6, 0) };
        var folderButton = new Button { Content = "📁 フォルダ" };

        // 名前入力欄
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        nameRow.Children.Add(new TextBlock { Text = "表示名", Width = 50 });
        var nameBox = new TextBox
        {
            PlaceholderText = "例：就業管理システム",
            Text = currentName
        };
        nameRow.Children.Add(nameBox);

        fileButton.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                linkBox.Text = file.Path;
                nameBox.Text = System.IO.Path.GetFileNameWithoutExtension(file.Path);
            }
        };

        folderButton.Click += async (_, _) =>
        {
            var picker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                linkBox.Text = folder.Path;
                nameBox.Text = System.IO.Path.GetFileName(folder.Path) ?? folder.DisplayName;
            }
        };

        var browseRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        browseRow.Children.Add(fileButton);
        browseRow.Children.Add(folderButton);

        var panel = new StackPanel { Spacing = 4, MinWidth = 400 };
        panel.Children.Add(linkRow);
        panel.Children.Add(browseRow);
        panel.Children.Add(nameRow);
        panel.Children.Add(new TextBlock
        {
            Text         = "URL を入力する場合は参照ボタンは使用せず直接入力してください。",
            FontSize     = 11,
            Foreground   = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Margin       = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            Title             = dialogTitle,
            Content           = panel,
            PrimaryButtonText = "OK",
            CloseButtonText   = "キャンセル",
            DefaultButton     = ContentDialogButton.Primary,
            XamlRoot          = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

        var name = nameBox.Text.Trim();
        var link = linkBox.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(link)) return null;
        return (name, link);
    }

    // ===========================
    // リンク起動・ユーティリティ
    // ===========================

    /// <summary>リンク先（ファイルパス・フォルダパス・URL）をOSのデフォルトアプリで起動する。</summary>
    private void LaunchLink(string link)
    {
        try
        {
            Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.Trace($"[AttendanceLogPage] リンク起動エラー: {link} / {ex.Message}");
        }
    }

    /// <summary>リンク先の種別に応じた Segoe MDL2 Assets のグリフ文字を返す。</summary>
    private static string GetLinkGlyph(string link)
    {
        if (link.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
         || link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "\uE774"; // 地球儀

        try
        {
            if (Directory.Exists(link)) return "\uE8B7"; // フォルダ
            if (File.Exists(link))      return "\uE7C3"; // ドキュメント
        }
        catch { }

        return "\uE71B"; // リンク（汎用）
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
