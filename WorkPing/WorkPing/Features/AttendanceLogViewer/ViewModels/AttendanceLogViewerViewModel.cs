using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Reactive.Disposables;
using WorkPing.Services;

namespace WorkPing.Features.AttendanceLogViewer.ViewModels;

/// <summary>
/// 勤怠ログ一覧ウィンドウの ViewModel（基本定義・コンストラクタ・Dispose）。
///
/// 役割：
/// - AttendanceLogService からエントリーを読み込む
/// - 日付範囲・名前・日付テキストでフィルタリングして FilteredEntries を更新する
/// </summary>
public partial class AttendanceLogViewerViewModel : IDisposable
{
    private readonly AttendanceLogService _attendanceLogService;

    // リアクティブリソースのまとめて Dispose 用
    public CompositeDisposable Disposable { get; } = new();

    // フィルタリング前の全エントリーを保持するバッファ
    private List<Models.AttendanceLogEntry> _allEntries = new();

    public AttendanceLogViewerViewModel(AttendanceLogService attendanceLogService)
    {
        _attendanceLogService = attendanceLogService;

        // CurrentDateRange が変わるたびにタイトルを更新する（ラベルをそのまま使う）
        CurrentDateRange.Subscribe(r => RangeTitle.Value = r).AddTo(Disposable);

        // フィルタテキストが変化したら自動で再フィルタリングする
        NameFilter.Subscribe(_ => ApplyFilter()).AddTo(Disposable);
        DateFilter.Subscribe(_ => ApplyFilter()).AddTo(Disposable);
    }

    public void Dispose() => Disposable.Dispose();

    // ===========================
    // データ読み込み
    // ===========================

    /// <summary>
    /// 指定されたファイルから全エントリーを読み込み、
    /// 指定された日付範囲で初期フィルタリングを行う。
    /// ウィンドウが開いたとき・ファイルが切り替わったときに呼び出す。
    /// </summary>
    /// <param name="filePath">XML ログファイルのパス</param>
    /// <param name="dateRange">初期日付範囲（"Today" / "Week" / "Month" / "All"）</param>
    public async Task LoadAsync(string filePath, string dateRange)
    {
        CurrentDateRange.Value = dateRange;
        _allEntries = await _attendanceLogService.ReadAllEntriesAsync(filePath);
        ApplyFilter();
    }

    /// <summary>
    /// 日付範囲だけを変更して再フィルタリングする。
    /// タイトルバーのドロップダウンで範囲が切り替わったときに呼び出す。
    /// </summary>
    /// <param name="dateRange">"Today" / "Week" / "Month" / "All"</param>
    public void ChangeDateRange(string dateRange)
    {
        CurrentDateRange.Value = dateRange;
        ApplyFilter();
    }

    // ===========================
    // フィルタリング
    // ===========================

    /// <summary>
    /// 日付範囲・名前テキスト・日付テキストの 3 条件を AND で組み合わせて
    /// FilteredEntries を再構築する。
    /// NameFilter / DateFilter の購読コールバックおよび LoadAsync / ChangeDateRange から呼ばれる。
    /// </summary>
    private void ApplyFilter()
    {
        var today = DateTime.Today;

        // --- 日付範囲フィルタ ---
        // Week  ：今週日曜日〜今週土曜日
        // Month ：今月1日〜今月末日
        var weekStart  = today.AddDays(-(int)today.DayOfWeek);          // 直近の日曜日
        var weekEnd    = weekStart.AddDays(6);                           // 直近の土曜日
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd   = new DateTime(today.Year, today.Month,
                             DateTime.DaysInMonth(today.Year, today.Month));

        Func<Models.AttendanceLogEntry, bool> rangeFilter = CurrentDateRange.Value switch
        {
            "Today" => e => e.Date == today,
            "Week"  => e => e.Date >= weekStart  && e.Date <= weekEnd,
            "Month" => e => e.Date >= monthStart && e.Date <= monthEnd,
            _       => _ => true   // "All"：すべて
        };

        // --- テキストフィルタ ---
        var nameTxt = NameFilter.Value?.Trim() ?? string.Empty;
        var dateTxt = DateFilter.Value?.Trim() ?? string.Empty;

        var filtered = _allEntries
            .Where(rangeFilter)
            .Where(e => string.IsNullOrEmpty(nameTxt)
                     || e.Name.Contains(nameTxt, StringComparison.OrdinalIgnoreCase))
            .Where(e => string.IsNullOrEmpty(dateTxt)
                     || e.DateDisplay.Contains(dateTxt))
            .ToList();

        // ObservableCollection を差し替える（Clear → AddRange）
        FilteredEntries.Clear();
        foreach (var entry in filtered)
        {
            FilteredEntries.Add(entry);
        }

        EntryCount.Value = $"{FilteredEntries.Count} 件";
    }
}
