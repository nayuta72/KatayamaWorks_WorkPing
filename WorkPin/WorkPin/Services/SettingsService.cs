using Reactive.Bindings;
using System.Text.Json;
using System.Text.Encodings.Web;
using WorkPin.Models;

namespace WorkPin.Services;

/// <summary>
/// アプリケーション設定（settings.json）の読み書きを担当するサービスクラス。
/// 設定は %AppData%\Roaming\kikakutools\WorkPin\settings.json に保存される。
/// Settings プロパティを ReactivePropertySlim として公開しているため、
/// アプリ内のどこからでも変更を購読できる。
/// </summary>
public class SettingsService
{
    // settings.json の保存ディレクトリ
    private readonly string _settingsDirectory;

    // settings.json のフルパス
    private readonly string _settingsFilePath;

    /// <summary>
    /// 現在の設定を保持するリアクティブプロパティ。
    /// 設定が変更されると購読しているすべてのコンポーネントに通知される。
    /// </summary>
    public ReactivePropertySlim<AppSettings> Settings { get; } = new(new AppSettings());

    public SettingsService()
    {
        // %AppData%\Roaming\kikakutools\WorkPin\ を設定ファイルの保存先とする
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsDirectory = Path.Combine(appData, "kikakutools", "WorkPin");
        _settingsFilePath  = Path.Combine(_settingsDirectory, "settings.json");
    }

    /// <summary>
    /// 設定ファイルを非同期で読み込む。
    /// ファイルが存在しない場合はデフォルト設定（未設定状態）を使用する。
    /// ディレクトリが存在しない場合は自動的に作成する。
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        try
        {
            // フォルダが存在しない場合は作成する
            if (!Directory.Exists(_settingsDirectory))
            {
                Directory.CreateDirectory(_settingsDirectory);
            }

            if (!File.Exists(_settingsFilePath))
            {
                // 初回起動時はデフォルト設定でファイルを新規作成する
                // 仕様：「フォルダが存在するかを確認し、なければ作成する」
                Settings.Value = new AppSettings();
                await SaveSettingsAsync();
                return;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            Settings.Value = settings ?? new AppSettings();

            // 日付が変わっていた場合は当日データをリセットして保存する
            // （前日の出退勤ステータス・保留フラグが残ったまま起動するのを防ぐ）
            await ResetDailyDataIfDateChangedAsync();
        }
        catch (Exception ex)
        {
            // 読み込みに失敗してもアプリを落とさない（デフォルト設定で続行）
            System.Diagnostics.Debug.WriteLine($"[SettingsService] 設定読み込みエラー: {ex.Message}");
            Settings.Value = new AppSettings();
        }
    }

    /// <summary>
    /// 現在の設定を非同期でファイルに保存する。
    /// </summary>
    /// <exception cref="Exception">ファイル書き込みに失敗した場合にスローされる。</exception>
    public async Task SaveSettingsAsync()
    {
        try
        {
            if (!Directory.Exists(_settingsDirectory))
            {
                Directory.CreateDirectory(_settingsDirectory);
            }

            var json = JsonSerializer.Serialize(Settings.Value, JsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] 設定保存エラー: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// LastLogDate が今日でない場合に InternalState の当日データをリセットして保存する。
    /// アプリ起動時に LoadSettingsAsync から呼ばれる。
    /// リセット対象: Today 系フィールドと HasPendingWrite（DefaultLogFileIndex と LastLogDate はそのまま）。
    /// </summary>
    private async Task ResetDailyDataIfDateChangedAsync()
    {
        var state   = Settings.Value.InternalState;
        var todayStr = DateTime.Today.ToString("yyyyMMdd");

        // LastLogDate が今日と一致している場合はリセット不要
        if (state.LastLogDate == todayStr) return;

        state.ResetDailyData();
        await SaveSettingsAsync();

        System.Diagnostics.Debug.WriteLine(
            $"[SettingsService] 日付変更を検出（{state.LastLogDate} → {todayStr}）。当日データをリセットしました。");
    }

    /// <summary>
    /// 設定の必須項目（姓・名・ユーザーID・ログパス）がすべて入力されているかを確認する。
    /// いずれか一つでも未設定の場合は false を返す。
    /// </summary>
    public bool IsSettingsValid()
    {
        var s = Settings.Value;
        return !string.IsNullOrWhiteSpace(s.LastName)
            && !string.IsNullOrWhiteSpace(s.FirstName)
            && !string.IsNullOrWhiteSpace(s.UserId)
            && s.LogFilePaths.Count > 0
            && s.LogFilePaths.Any(p => !string.IsNullOrWhiteSpace(p.FilePath));
    }

    // JSON シリアライズ設定（日本語の文字をそのまま保存、インデントあり）
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
