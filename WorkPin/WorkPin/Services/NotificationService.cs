using Microsoft.Win32;
using System.Diagnostics;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace WorkPin.Services;

/// <summary>
/// Windows のトースト通知（画面右下のポップアップ）を送信するサービスクラス。
///
/// 実装方針：
///   アンパッケージドアプリでは Windows App SDK の AppNotificationManager.Register() が
///   COM レベルのクラッシュを引き起こすため、旧来の WinRT API
///   「Windows.UI.Notifications.ToastNotificationManager」を使用する。
///
///   アンパッケージドアプリが ToastNotificationManager を使うには、
///   HKCU\SOFTWARE\Classes\AppUserModelId\ にアプリの識別子（AUMID）を
///   レジストリ登録する必要がある（管理者権限不要・アプリ起動時に自動登録）。
///
/// 通知アイコン：
///   StatusIconService で生成した色付き丸アイコン PNG を
///   appLogoOverride + hint-crop="circle" で表示する。
///   ステータスが不明の場合はアイコンなしで通知する。
/// </summary>
public class NotificationService
{
    // アプリの識別子（AUMID）。レジストリ登録と通知送信の両方で使用する
    private const string AppId = "KikakuTools.WorkPin";

    // 通知の送信に使用する ToastNotifier（Initialize() で生成する）
    private ToastNotifier? _notifier;

    // 初期化が完了しているかどうかのフラグ
    private bool _isInitialized = false;

    /// <summary>
    /// 通知サービスを初期化する。
    /// レジストリに AUMID を登録し、ToastNotifier を生成する。
    /// ステータスアイコン PNG も同時に生成する。
    /// App.xaml.cs の OnLaunched から呼ぶこと。
    /// </summary>
    public void Initialize()
    {
        try
        {
            // ステータス別アイコン PNG を生成する（%TEMP%\WorkPin\icons\ に保存）
            StatusIconService.Initialize();

            // アプリの AUMID を HKCU レジストリに登録する
            // → 管理者権限不要、アプリ起動のたびに上書きするが実害はない
            RegisterAumid();

            // 登録した AUMID で ToastNotifier を生成する
            _notifier = ToastNotificationManager.CreateToastNotifier(AppId);
            _isInitialized = true;

            Debug.WriteLine("[NotificationService] 通知サービスを初期化しました。");
        }
        catch (Exception ex)
        {
            // 初期化に失敗してもアプリを落とさない（通知が使えなくなるだけ）
            Debug.WriteLine($"[NotificationService] 初期化エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// トースト通知を送信する。
    /// </summary>
    /// <param name="title">通知のタイトル（例：WorkPin）</param>
    /// <param name="message">通知の本文（例：山田太郎 が出勤しました）</param>
    /// <param name="status">
    ///   表示するステータスアイコンの種別（◎/◯/✕）。
    ///   null の場合はアイコンなしで通知する。
    /// </param>
    public void ShowNotification(string title, string message, string? status = null)
    {
        if (!_isInitialized || _notifier == null)
        {
            Debug.WriteLine("[NotificationService] 未初期化のため通知をスキップします。");
            return;
        }

        try
        {
            // ステータスに対応するアイコン URI を取得する（null = アイコンなし）
            var iconUri = StatusIconService.GetIconUri(status);

            // XML 文字列でトーストを組み立てる
            // appLogoOverride + hint-crop="circle" で丸アイコンを表示する
            var xml = BuildToastXml(title, message, iconUri);

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            var notification = new ToastNotification(xmlDoc);
            _notifier.Show(notification);

            Debug.WriteLine($"[NotificationService] 通知を送信しました: {title} / {message} / status={status}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationService] 通知送信エラー: {ex.Message}");
        }
    }

    // ───────────────────────────────────────────
    // 内部実装
    // ───────────────────────────────────────────

    /// <summary>
    /// トースト XML 文字列を組み立てる。
    /// iconUri が指定された場合は appLogoOverride として丸アイコンを追加する。
    /// XML 特殊文字（&lt;, &gt;, &amp;, &quot;）はエスケープする。
    /// </summary>
    private static string BuildToastXml(string title, string message, string? iconUri)
    {
        var escapedTitle   = EscapeXml(title);
        var escapedMessage = EscapeXml(message);

        // アイコンがある場合: appLogoOverride で丸クロップアイコンを表示する
        var logoElement = iconUri != null
            ? $"""<image placement="appLogoOverride" hint-crop="circle" src="{EscapeXml(iconUri)}" />"""
            : string.Empty;

        return $"""
            <toast>
              <visual>
                <binding template="ToastGeneric">
                  <text>{escapedTitle}</text>
                  <text>{escapedMessage}</text>
                  {logoElement}
                </binding>
              </visual>
            </toast>
            """;
    }

    /// <summary>XML の特殊文字をエスケープする。</summary>
    private static string EscapeXml(string value) =>
        value
            .Replace("&",  "&amp;")
            .Replace("<",  "&lt;")
            .Replace(">",  "&gt;")
            .Replace("\"", "&quot;");

    /// <summary>
    /// アプリの AUMID を HKCU レジストリに登録する。
    /// ToastNotificationManager.CreateToastNotifier(AppId) の呼び出し前に必須。
    ///
    /// 登録先：HKCU\SOFTWARE\Classes\AppUserModelId\KikakuTools.WorkPin
    ///   DisplayName : "WorkPin"
    ///   IconUri     : 実行ファイルのパス（通知アイコンとして使用）
    /// </summary>
    private static void RegisterAumid()
    {
        var keyPath = $@"SOFTWARE\Classes\AppUserModelId\{AppId}";
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);

        key.SetValue("DisplayName", "WorkPin");

        // 通知領域に表示するアイコンとして実行ファイルのパスを登録する
        var exePath = Environment.ProcessPath ?? string.Empty;
        if (!string.IsNullOrEmpty(exePath))
            key.SetValue("IconUri", exePath);
    }
}
