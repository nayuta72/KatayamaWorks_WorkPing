using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using Windows.Storage.Pickers;
using WorkPin.Features.Shortcut.ViewModels;
using WorkPin.Models;

namespace WorkPin.Features.Shortcut.Views;

/// <summary>
/// ショートカットページのコードビハインド。
///
/// 動作概要：
///   - ページ表示時に settings.json からショートカットを読み込み、
///     WrapPanel（ShortcutPanel）にボタンを動的に生成して追加する。
///   - ボタンは横3列で左から右・上から下に並ぶ（幅 210px × 3 ≈ 650px）。
///   - 右クリックメニューで「編集」「削除」が可能。
///   - 「ショートカットを追加」ボタンで ContentDialog を表示して新規登録できる。
/// </summary>
public sealed partial class ShortcutPage : Page
{
    // ボタン1個のサイズ（横3列に並ぶ程度の幅）
    private const double ButtonWidth   = 180;
    private const double ButtonHeight  = 40;
    private const double ButtonMargin  = 4;
    // 1行に並べるボタンの最大数
    private const int    ColumnsPerRow = 3;

    /// <summary>このページにバインドされた ViewModel。</summary>
    public ShortcutViewModel ViewModel { get; }

    public ShortcutPage()
    {
        InitializeComponent();
        ViewModel = App.ServiceProvider.GetRequiredService<ShortcutViewModel>();
    }

    // ===========================
    // ページ読み込み
    // ===========================

    /// <summary>
    /// ページが表示されるたびに settings.json を再読み込みしてボタンを再構築する。
    /// NavigationCacheMode="Disabled" のため、ページ遷移のたびにここが呼ばれる。
    /// </summary>
    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadFromSettings();
        BuildShortcutButtons();
    }

    // ===========================
    // ショートカットボタン動的生成
    // ===========================

    /// <summary>
    /// ShortcutPanel のショートカットボタン行を再構築する。
    ///
    /// 【方針】
    ///   XAML で「追加」ボタンが ShortcutPanel の最後の子として固定配置されている。
    ///   このメソッドでは「追加」ボタン（最後の1個）を残しつつ、
    ///   それ以外の行 StackPanel をすべて削除してから
    ///   ViewModel.Shortcuts の順番（= Settings.json の並び順）で
    ///   追加ボタンの前に挿入することで「追加ボタンが常に末尾」を実現する。
    /// </summary>
    private void BuildShortcutButtons()
    {
        // 「追加」ボタン（ShortcutPanel の最後の子）を残し、
        // それより前にある行 StackPanel をすべて削除する
        while (ShortcutPanel.Children.Count > 1)
            ShortcutPanel.Children.RemoveAt(0);

        // ショートカットを Settings.json の並び順（ViewModel.Shortcuts の順）で
        // 追加ボタンの前に挿入する
        int insertIndex = 0;   // 次に挿入する位置（追加ボタンは常に末尾）
        StackPanel? currentRow = null;
        int columnIndex = 0;

        foreach (var item in ViewModel.Shortcuts)
        {
            // ColumnsPerRow 個ごとに新しい行 StackPanel を追加ボタンの前に挿入する
            if (columnIndex % ColumnsPerRow == 0)
            {
                currentRow = new StackPanel { Orientation = Orientation.Horizontal };
                ShortcutPanel.Children.Insert(insertIndex, currentRow);
                insertIndex++;
            }

            currentRow!.Children.Add(CreateShortcutButton(item));
            columnIndex++;
        }
    }

    /// <summary>
    /// ショートカットアイテム1件分のボタンを生成する。
    ///   - クリック → リンク先を起動
    ///   - 右クリック → 編集 / 削除 メニュー
    ///   - 上段にリンク種別アイコン＋名前、下段にリンク先パス（省略表示）
    /// </summary>
    private Button CreateShortcutButton(ShortcutItem item)
    {
        // ── ボタン内コンテンツ ───────────────────────────────────────
        // 上段：アイコン ＋ 名前（太字）
        var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var icon = new FontIcon
        {
            Glyph    = GetLinkGlyph(item.Link),
            FontSize = 14,
        };
        var nameBlock = new TextBlock
        {
            Text         = item.Name,
            FontWeight   = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth     = ButtonWidth - 40,
            VerticalAlignment = VerticalAlignment.Center
        };
        namePanel.Children.Add(icon);
        namePanel.Children.Add(nameBlock);

        // 下段：リンク先（小さいフォント・省略表示）
/*
        var linkBlock = new TextBlock
        {
            Text         = item.Link,
            FontSize     = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth     = ButtonWidth - 20,
            Foreground   = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
*/
        var content = new StackPanel
        {
            Spacing             = 2,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        content.Children.Add(namePanel);
//        content.Children.Add(linkBlock);

        // ── ボタン本体 ───────────────────────────────────────────────
        var button = new Button
        {
            Width                    = ButtonWidth,
            Height                   = ButtonHeight,
            Margin                   = new Thickness(ButtonMargin),
            Content                  = content,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };

        // クリックでリンクを開く
        button.Click += (_, _) => LaunchLink(item.Link);

        // ── 右クリックメニュー ──────────────────────────────────────
        var flyout    = new MenuFlyout();
        var editItem  = new MenuFlyoutItem { Text = "編集" };
        var separator = new MenuFlyoutSeparator();
        var delItem   = new MenuFlyoutItem { Text = "削除" };

        editItem.Click += async (_, _) => await EditShortcutAsync(item);
        delItem.Click  += async (_, _) => await DeleteShortcutAsync(item);

        flyout.Items.Add(editItem);
        flyout.Items.Add(separator);
        flyout.Items.Add(delItem);
        button.ContextFlyout = flyout;

        return button;
    }

    // ===========================
    // ショートカット追加ダイアログ
    // ===========================

    private async void AddShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ShowShortcutDialogAsync("ショートカットを追加");
        if (result == null) return;

        await ViewModel.AddShortcutAsync(result.Value.Name, result.Value.Link);
        BuildShortcutButtons();
    }

    // ===========================
    // 編集・削除
    // ===========================

    /// <summary>
    /// 右クリックメニューの「編集」が押されたときの処理。
    /// ダイアログを現在の値で初期化して表示し、変更があれば保存してボタンを再構築する。
    /// </summary>
    private async Task EditShortcutAsync(ShortcutItem item)
    {
        var result = await ShowShortcutDialogAsync("ショートカットを編集",
                                                    item.Name, item.Link);
        if (result == null) return;

        await ViewModel.UpdateShortcutAsync(item, result.Value.Name, result.Value.Link);
        BuildShortcutButtons();
    }

    /// <summary>
    /// 右クリックメニューの「削除」が押されたときの処理。
    /// 確認ダイアログを表示してから削除する。
    /// </summary>
    private async Task DeleteShortcutAsync(ShortcutItem item)
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

        var result = await confirmDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.DeleteShortcutAsync(item);
        BuildShortcutButtons();
    }

    // ===========================
    // 入力ダイアログ（追加・編集共用）
    // ===========================

    /// <summary>
    /// 名前とリンク先を入力する ContentDialog を表示する。
    /// 追加時は currentName/currentLink を空に、編集時は既存値を渡す。
    /// キャンセルまたは必須項目が空の場合は null を返す。
    /// </summary>
    private async Task<(string Name, string Link)?> ShowShortcutDialogAsync(
        string dialogTitle,
        string currentName = "",
        string currentLink = "")
    {
        // ── 入力欄 ───────────────────────────────────────────────────
        var nameBox = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2, 0, 2)
        };
        var nameBox0 = new TextBlock
        {
            Text = "名前",
            Width = 50
        };
        var nameBox1 = new TextBox
        {
            PlaceholderText = "例：共有フォルダ、社内ポータル",
            Text            = currentName,
            Margin          = new Thickness(0, 0, 0, 0)
        };
        nameBox.Children.Add(nameBox0);
        nameBox.Children.Add(nameBox1);

        var linkBox = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2, 0, 2)
        };
        var linkBox0 = new TextBlock
        {
            Text = "リンク先",
            Width = 50
        };
        var linkBox1 = new TextBox
        {
            PlaceholderText = "\\\\server\\share\\folder  /  C:\\Users\\...  /  https://...",
            Text            = currentLink
        };
        linkBox.Children.Add(linkBox0);
        linkBox.Children.Add(linkBox1);

        // ── 参照ボタン（ファイル / フォルダ）───────────────────────────
        var fileButton   = new Button { Content = "📄 ファイル", Margin = new Thickness(0, 0, 6, 0) };
        var folderButton = new Button { Content = "📁 フォルダ" };

        fileButton.Click += async (_, _) =>
        {
            // ファイル選択ピッカー
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");
            var file = await picker.PickSingleFileAsync();
            if (file != null) linkBox1.Text = file.Path;
        };

        folderButton.Click += async (_, _) =>
        {
            // フォルダ選択ピッカー
            var picker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null) linkBox1.Text = folder.Path;
        };

        var browsePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 2, 0, 0)
        };
        browsePanel.Children.Add(fileButton);
        browsePanel.Children.Add(folderButton);

        // ── ダイアログコンテンツ ─────────────────────────────────────
        var panel = new StackPanel
        {
            Spacing  = 4,
            MinWidth = 400
        };
        panel.Children.Add(nameBox);
        panel.Children.Add(linkBox);
        panel.Children.Add(browsePanel);
        panel.Children.Add(new TextBlock
        {
            Text      = "URL を入力する場合は参照ボタンは使用せず直接入力してください。",
            FontSize  = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Margin    = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        // ── ダイアログ表示 ───────────────────────────────────────────
        var dialog = new ContentDialog
        {
            Title             = dialogTitle,
            Content           = panel,
            PrimaryButtonText = "OK",
            CloseButtonText   = "キャンセル",
            DefaultButton     = ContentDialogButton.Primary,
            XamlRoot          = XamlRoot
        };

        var dialogResult = await dialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary) return null;

        var name = nameBox1.Text.Trim();
        var link = linkBox1.Text.Trim();

        // 名前とリンクのどちらかが空なら無効とみなす
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(link)) return null;

        return (name, link);
    }

    // ===========================
    // リンク起動
    // ===========================

    /// <summary>
    /// リンク先（ファイルパス・フォルダパス・URL）をOSのデフォルトアプリで起動する。
    /// UseShellExecute = true にすることでファイル・フォルダ・URL をまとめて処理できる。
    /// </summary>
    private void LaunchLink(string link)
    {
        try
        {
            Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ShortcutPage] リンク起動エラー: {link} / {ex.Message}");
            // エラー通知は次のディスパッチサイクルで ContentDialog を表示する（async void で対応）
            _ = ShowLaunchErrorAsync(link, ex.Message);
        }
    }

    private async Task ShowLaunchErrorAsync(string link, string errorMessage)
    {
        var dialog = new ContentDialog
        {
            Title             = "リンクを開けませんでした",
            Content           = $"リンク先: {link}\n\nエラー: {errorMessage}",
            CloseButtonText   = "閉じる",
            XamlRoot          = XamlRoot
        };
        await dialog.ShowAsync();
    }

    // ===========================
    // ユーティリティ
    // ===========================

    /// <summary>
    /// リンク先の種別に応じた Segoe MDL2 Assets のグリフ文字を返す。
    ///   URL（http/https）→ 地球儀アイコン
    ///   フォルダ         → フォルダアイコン
    ///   ファイル         → ドキュメントアイコン
    ///   不明             → リンクアイコン
    /// </summary>
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
        catch { /* ネットワークパスで例外が出る場合は無視 */ }

        return "\uE71B"; // リンク（汎用）
    }
}
