using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using WorkPing.Features.AttendanceLog.Models;
// ColorMatrix / ImageAttributes を使うために必要

namespace WorkPing.Services;

/// <summary>
/// 出退勤ステータスに対応した丸アイコン PNG を生成・管理するサービスクラス。
///
/// トースト通知の appLogoOverride に画像 URI として渡すために使用する。
/// Windows.UI.Notifications はアプリ内の XAML コントロールを描画できないため、
/// System.Drawing（GDI+）で PNG を動的生成し file:// URI で参照する。
///
/// アイコンの仕様：
///   円の背景色 = 絵文字（😊😐😞）の顔色（黄色 #FFD700）を白と 50% ブレンドした
///               パステル黄（#FFEB7F）。3つとも同じ色で統一する。
///   中央の絵文字 = ステータスに対応する顔絵文字（Segoe UI Emoji フォントで描画）
///               ◯ → 😊 / △ → 😐 / ✕ → 😞
///
/// 生成された PNG は %TEMP%\WorkPing\icons\ に保存される。
/// アプリ起動時に毎回上書きするため、exe のバージョン変更にも追従する。
/// </summary>
public static class StatusIconService
{
    // 出力先フォルダ（%TEMP%\WorkPing\icons\）
    private static readonly string IconDirectory =
        Path.Combine(Path.GetTempPath(), "WorkPing", "icons");

    // ステータスと PNG ファイルパスのキャッシュ（Initialize() で生成後に設定する）
    private static readonly Dictionary<string, string> _iconPaths = new();

    // アイコンサイズ（px）。トースト通知の appLogoOverride 推奨サイズ
    private const int IconSize = 64;

    // 絵文字のフォントサイズ（px）
    private const float EmojiFontSize = 36f;

    // 絵文字の不透明度（0.0 = 完全透明 / 1.0 = 完全不透明）
    private const float EmojiOpacity = 0.7f;

    /// <summary>
    /// ボタンの各色（緑/ゴールデンロッド/クリムゾン）を白と 50% ブレンドしたパステル色。
    /// 円の背景色としてステータスごとに使い分ける。
    ///
    /// 計算（各色を白 #FFFFFF と 50% ブレンド）:
    ///   Green      #008000 = (  0, 128,   0) → (127, 191, 127) = #7FBF7F（淡い緑）
    ///   Goldenrod  #DAA520 = (218, 165,  32) → (236, 210, 143) = #ECD28F（淡い黄）
    ///   Crimson    #DC143C = (220,  20,  60) → (237, 137, 157) = #ED899D（淡い赤）
    /// </summary>
    private static readonly Color PastelGreen  = Color.FromArgb(127, 191, 127); // 淡い緑
    private static readonly Color PastelYellow = Color.FromArgb(236, 210, 143); // 淡い黄
    private static readonly Color PastelRed    = Color.FromArgb(237, 137, 157); // 淡い赤
    private static readonly Color PastelBlue   = Color.FromArgb(143, 188, 219); // 淡い青（コメント用）

    /// <summary>コメントアイコンのキー（ステータス文字とは別の固定キーで管理する）</summary>
    public const string CommentKey = "comment";

    /// <summary>
    /// アイコン PNG を生成してキャッシュする。
    /// NotificationService.Initialize() から呼び出すこと。
    /// 既にファイルが存在していても上書きする（exe 更新対応）。
    /// </summary>
    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(IconDirectory);

            // 3つのステータスごとにアイコンを生成する（円の色はステータスで使い分ける）
            Generate(AttendanceLogModel.StatusGood,   PastelGreen,  "😊"); // 淡い緑 + 笑顔
            Generate(AttendanceLogModel.StatusNormal, PastelYellow, "😐"); // 淡い黄 + 真顔
            Generate(AttendanceLogModel.StatusBad,    PastelRed,    "😞"); // 淡い赤 + 辛い顔
            // コメント通知用アイコン（吹き出し絵文字・淡い青の円）
            Generate(CommentKey, PastelBlue, "💬");

            Debug.WriteLine($"[StatusIconService] アイコンを生成しました: {IconDirectory}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StatusIconService] アイコン生成エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// ステータス文字（◯/△/✕）に対応するアイコン PNG の file:// URI を返す。
    /// アイコンが存在しない場合は null を返す。
    /// </summary>
    /// <param name="status">◯ / △ / ✕。null または未知の値の場合は null を返す。</param>
    public static string? GetIconUri(string? status)
    {
        if (status == null || !_iconPaths.TryGetValue(status, out var path))
            return null;

        // toast の src 属性は "file:///" 形式が必要（バックスラッシュはスラッシュに変換）
        return "file:///" + path.Replace('\\', '/');
    }

    // ───────────────────────────────────────────
    // 内部実装
    // ───────────────────────────────────────────

    /// <summary>
    /// 指定した色の円の上に絵文字を描いた PNG を生成する。
    /// </summary>
    /// <param name="status">ステータス文字（◯/△/✕）。ファイル名とキャッシュキーに使用する。</param>
    /// <param name="circleColor">円の背景色（パステル緑/黄/赤）</param>
    /// <param name="emoji">中央に描画する絵文字（😊 / 😐 / 😞）</param>
    private static void Generate(string status, Color circleColor, string emoji)
    {
        var filePath = Path.Combine(IconDirectory, $"status_{status}.png");

        using var bitmap = new Bitmap(IconSize, IconSize, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // 透明でクリア
        g.Clear(Color.Transparent);

        // 指定色の円を描画する（1px の余白でアンチエイリアスを活かす）
        using var circleBrush = new SolidBrush(circleColor);
        g.FillEllipse(circleBrush, 1, 1, IconSize - 2, IconSize - 2);

        // ── 絵文字を 70% 透明度で描画する ────────────────────────────────
        // カラー絵文字（Segoe UI Emoji）はフォント自身が色を持つため、
        // SolidBrush のアルファを変えても透明度が効かない。
        // そのため「別の透明ビットマップに絵文字を描いてから
        // ColorMatrix でアルファを掛けて本ビットマップに合成する」手順を踏む。

        using var font = new Font("Segoe UI Emoji", EmojiFontSize,
                                  FontStyle.Regular, GraphicsUnit.Pixel);

        // ① 絵文字を透明な一時ビットマップに描画する
        using var emojiBmp = new Bitmap(IconSize, IconSize, PixelFormat.Format32bppArgb);
        using (var eg = Graphics.FromImage(emojiBmp))
        {
            eg.SmoothingMode     = SmoothingMode.AntiAlias;
            eg.TextRenderingHint = TextRenderingHint.AntiAlias;
            eg.Clear(Color.Transparent);

            // 中央揃えの座標を計算する
            var textSize = eg.MeasureString(emoji, font);
            float x = (IconSize - textSize.Width)  / 2f;
            float y = (IconSize - textSize.Height) / 2f;
            eg.DrawString(emoji, font, Brushes.Black, x, y);
        }

        // ② ColorMatrix でアルファ係数を EmojiOpacity（0.7）に設定する
        var colorMatrix = new ColorMatrix();
        colorMatrix.Matrix33 = EmojiOpacity; // Matrix33 = アルファ成分の倍率

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(colorMatrix,
                                  ColorMatrixFlag.Default,
                                  ColorAdjustType.Bitmap);

        // ③ 透明度を適用しながら一時ビットマップを本ビットマップへ合成する
        g.DrawImage(emojiBmp,
                    new Rectangle(0, 0, IconSize, IconSize),
                    0, 0, IconSize, IconSize,
                    GraphicsUnit.Pixel,
                    attributes);

        bitmap.Save(filePath, ImageFormat.Png);

        // ファイルパスをキャッシュに登録する
        _iconPaths[status] = filePath;
    }
}
