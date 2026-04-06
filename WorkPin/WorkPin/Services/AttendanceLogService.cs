using System.Globalization;
using System.Xml.Linq;
using WorkPin.Features.AttendanceLogViewer.Models;
using WorkPin.Models;

namespace WorkPin.Services;

/// <summary>
/// 出退勤ログの XML ファイルへの読み書きを担当するサービスクラス。
///
/// XML 構造：
/// &lt;Root&gt;
///   &lt;E20260404_U001 Name="山田太郎" Type="出社" ClockIn="09:00" ClockInStatus="◎"
///                   ClockOut="18:00" ClockOutStatus="◯" Comment="" /&gt;
///   ...
///   &lt;LastLog Name="..." Type="..." ClockIn="..." ... /&gt;   ← 常に最後尾に存在
/// &lt;/Root&gt;
///
/// エレメント名は "E{yyyyMMdd}_{UserId}" 形式。
/// XML 仕様で要素名は数字始まりが禁止されているため、先頭に "E" を付与している。
/// </summary>
public class AttendanceLogService
{
    private readonly SettingsService _settingsService;

    public AttendanceLogService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// 出退勤エントリーを XML ファイルに書き込む。
    /// 同じ日付・ユーザーIDのエレメントが存在する場合は属性を上書きし、
    /// 存在しない場合は Root 直下の先頭（LastLog の前）に新規作成する。
    /// 書き込みと同時に LastLog エレメントも更新する。
    /// </summary>
    /// <param name="entry">書き込む出退勤エントリー</param>
    /// <param name="filePath">
    ///   書き込み先の XML ファイルパス。
    ///   null の場合は現在の設定（DefaultLogFileIndex）から取得する。
    /// </param>
    public async Task WriteEntryAsync(AttendanceEntry entry, string? filePath = null)
    {
        var targetPath = filePath ?? _settingsService.Settings.Value.CurrentLogFilePath?.FilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException("ログファイルのパスが設定されていません。アカウント設定でパスを登録してください。");
        }

        await Task.Run(() =>
        {
            // ファイルが存在しない場合は Root エレメントのみの XML を新規作成する
            XDocument doc;
            if (!File.Exists(targetPath))
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                doc = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("Root"));
            }
            else
            {
                doc = XDocument.Load(targetPath);
            }

            var root = doc.Root!;

            // 同じ日付・ユーザーIDのエレメントを検索する
            var existingElement = root.Element(entry.ElementName);

            if (existingElement != null)
            {
                // 既存エレメントの属性を更新する（入力されている項目のみ上書き）
                UpdateElementAttributes(existingElement, entry);
            }
            else
            {
                // 新規エレメントを Root 直下の先頭に追加する
                // ※ LastLog は常に最後尾に配置するため、先頭挿入は LastLog より前になる
                var newElement = CreateElement(entry);
                var firstChild = root.Elements()
                    .FirstOrDefault(e => e.Name.LocalName != "LastLog");

                if (firstChild != null)
                {
                    firstChild.AddBeforeSelf(newElement);
                }
                else
                {
                    // LastLog しかない、または空の場合は先頭に追加
                    root.AddFirst(newElement);
                }
            }

            // LastLog エレメントを Root の最後尾に更新する
            UpdateLastLog(root, entry);

            doc.Save(targetPath);

            // 書き込み成功後にバックアップファイル（名前_back.xml）を作成する。
            // 万が一ログファイルが壊れたときに手動で復元できるよう、
            // 正常書き込みが完了した時点の内容を別ファイルに退避しておく。
            CreateBackup(targetPath);
        });
    }

    /// <summary>
    /// 書き込み成功後に呼ばれるバックアップ作成メソッド。
    /// "{ファイル名}_back.xml" をログファイルと同じディレクトリに作成する。
    /// 例）Log2026.xml → Log2026_back.xml
    /// バックアップ中にエラーが発生してもメインの書き込みには影響しない。
    /// </summary>
    /// <param name="sourcePath">正常書き込みが完了したログファイルのパス</param>
    private static void CreateBackup(string sourcePath)
    {
        try
        {
            var dir      = Path.GetDirectoryName(sourcePath)  ?? string.Empty;
            var nameOnly = Path.GetFileNameWithoutExtension(sourcePath);
            var backPath = Path.Combine(dir, $"{nameOnly}_back.xml");

            File.Copy(sourcePath, backPath, overwrite: true);

            System.Diagnostics.Debug.WriteLine(
                $"[AttendanceLogService] バックアップを作成しました: {backPath}");
        }
        catch (Exception ex)
        {
            // バックアップ失敗はログに記録するだけでアプリの動作を止めない
            System.Diagnostics.Debug.WriteLine(
                $"[AttendanceLogService] バックアップ作成エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// 指定したファイルの LastLog エレメントを取得する。
    /// ファイルが存在しない場合や LastLog がない場合は null を返す。
    /// </summary>
    /// <param name="filePath">対象のXMLファイルパス</param>
    public async Task<XElement?> GetLastLogAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        return await Task.Run(() =>
        {
            try
            {
                var doc = XDocument.Load(filePath);
                return doc.Root?.Element("LastLog");
            }
            catch
            {
                return null;
            }
        });
    }

    /// <summary>
    /// 指定した XML ファイルの全エントリーを読み込む。
    /// LastLog エレメントは除外し、日付の新しい順に並べて返す。
    /// エレメント名 "E{yyyyMMdd}_{UserId}" から日付を解析する。
    /// </summary>
    /// <param name="filePath">読み込む XML ファイルのパス</param>
    /// <returns>全エントリーのリスト（日付降順）。ファイルが存在しない場合は空リスト。</returns>
    public async Task<List<AttendanceLogEntry>> ReadAllEntriesAsync(string filePath)
    {
        if (!File.Exists(filePath)) return new List<AttendanceLogEntry>();

        return await Task.Run(() =>
        {
            try
            {
                var doc  = XDocument.Load(filePath);
                var root = doc.Root;
                if (root == null) return new List<AttendanceLogEntry>();

                var entries = new List<AttendanceLogEntry>();

                foreach (var element in root.Elements())
                {
                    var localName = element.Name.LocalName;

                    // LastLog エレメントは除外する
                    if (localName == "LastLog") continue;

                    // エレメント名 "E{yyyyMMdd}_{UserId}" から日付部分（8文字）を取得する
                    // E + 8桁の日付 + _ + UserId の形式が必須
                    if (!localName.StartsWith("E") || localName.Length < 10) continue;

                    var datePart = localName.Substring(1, 8); // "20260404"
                    if (!DateTime.TryParseExact(datePart, "yyyyMMdd",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        continue;

                    entries.Add(new AttendanceLogEntry
                    {
                        Date           = date,
                        Name           = element.Attribute("Name")?.Value          ?? string.Empty,
                        WorkType       = element.Attribute("Type")?.Value          ?? string.Empty,
                        ClockIn        = element.Attribute("ClockIn")?.Value       ?? string.Empty,
                        ClockInStatus  = element.Attribute("ClockInStatus")?.Value ?? string.Empty,
                        ClockOut       = element.Attribute("ClockOut")?.Value      ?? string.Empty,
                        ClockOutStatus = element.Attribute("ClockOutStatus")?.Value ?? string.Empty,
                        Comment        = element.Attribute("Comment")?.Value       ?? string.Empty,
                    });
                }

                // 日付の新しい順、同日内は氏名順でソートする
                return entries
                    .OrderByDescending(e => e.Date)
                    .ThenBy(e => e.Name)
                    .ToList();
            }
            catch
            {
                return new List<AttendanceLogEntry>();
            }
        });
    }

    // ===========================
    // XML 操作ヘルパーメソッド
    // ===========================

    /// <summary>エントリーから新しい XElement を生成する。</summary>
    private static XElement CreateElement(AttendanceEntry entry)
    {
        var element = new XElement(entry.ElementName);
        UpdateElementAttributes(element, entry);
        return element;
    }

    /// <summary>
    /// エレメントの属性を更新する。
    /// null の項目は更新しない（既存の値を保持する）。
    /// </summary>
    private static void UpdateElementAttributes(XElement element, AttendanceEntry entry)
    {
        element.SetAttributeValue("Name", entry.Name);
        element.SetAttributeValue("Type", entry.WorkType);

        // null でない項目のみ更新する（部分更新を可能にする）
        if (entry.ClockInTime    != null) element.SetAttributeValue("ClockIn",       entry.ClockInTime);
        if (entry.ClockInStatus  != null) element.SetAttributeValue("ClockInStatus", entry.ClockInStatus);
        if (entry.ClockOutTime   != null) element.SetAttributeValue("ClockOut",      entry.ClockOutTime);
        if (entry.ClockOutStatus != null) element.SetAttributeValue("ClockOutStatus",entry.ClockOutStatus);
        if (entry.Comment        != null) element.SetAttributeValue("Comment",       entry.Comment);
    }

    /// <summary>
    /// LastLog エレメントを Root の最後尾に更新する。
    /// 既存の LastLog は削除して新規作成する（属性を一度すべてリセットするため）。
    ///
    /// 書き込む属性は entry.Action の種別によって決まる：
    ///   ClockIn  → Name / Type / ClockIn / ClockInStatus
    ///   ClockOut → Name / Type / ClockOut / ClockOutStatus
    ///   Comment  → Name / Comment
    /// </summary>
    private static void UpdateLastLog(XElement root, AttendanceEntry entry)
    {
        // 既存の LastLog を削除する（属性をすべてリセットするための処理）
        root.Element("LastLog")?.Remove();

        // 操作の種別に応じて書き込む属性を選択する
        var lastLog = new XElement("LastLog");

        switch (entry.Action)
        {
            case Models.AttendanceAction.ClockIn:
                lastLog.SetAttributeValue("Name",          entry.Name);
                lastLog.SetAttributeValue("Type",          entry.WorkType);
                lastLog.SetAttributeValue("ClockIn",       entry.ClockInTime    ?? string.Empty);
                lastLog.SetAttributeValue("ClockInStatus", entry.ClockInStatus  ?? string.Empty);
                break;

            case Models.AttendanceAction.ClockOut:
                lastLog.SetAttributeValue("Name",           entry.Name);
                lastLog.SetAttributeValue("Type",           entry.WorkType);
                lastLog.SetAttributeValue("ClockOut",       entry.ClockOutTime   ?? string.Empty);
                lastLog.SetAttributeValue("ClockOutStatus", entry.ClockOutStatus ?? string.Empty);
                break;

            case Models.AttendanceAction.Comment:
                lastLog.SetAttributeValue("Name",    entry.Name);
                lastLog.SetAttributeValue("Comment", entry.Comment ?? string.Empty);
                break;
        }

        root.Add(lastLog);
    }
}
