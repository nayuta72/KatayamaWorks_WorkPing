using System.Collections.ObjectModel;
using Reactive.Bindings;
using WorkPin.Features.AttendanceLogViewer.Models;

namespace WorkPin.Features.AttendanceLogViewer.ViewModels;

/// <summary>
/// AttendanceLogViewerViewModel のリアクティブプロパティ定義。
/// </summary>
public partial class AttendanceLogViewerViewModel
{
    /// <summary>
    /// 現在選択されている日付範囲（"Today" / "Week" / "Month" / "All"）。
    /// タイトルバーのドロップダウンで変更される。
    /// </summary>
    public ReactivePropertySlim<string> CurrentDateRange { get; } = new("Today");

    /// <summary>
    /// ウィンドウ内タイトルバーに表示する日付範囲の日本語タイトル。
    /// CurrentDateRange が変わるたびにコンストラクタの Subscribe で更新される。
    /// </summary>
    public ReactivePropertySlim<string> RangeTitle { get; } = new("Today");

    /// <summary>
    /// 名前フィルタの入力テキスト。
    /// ウィンドウ上部の TextBox に TwoWay バインドする。
    /// </summary>
    public ReactivePropertySlim<string> NameFilter { get; } = new(string.Empty);

    /// <summary>
    /// 日付フィルタの入力テキスト（例: "2026/04"）。
    /// ウィンドウ上部の TextBox に TwoWay バインドする。
    /// </summary>
    public ReactivePropertySlim<string> DateFilter { get; } = new(string.Empty);

    /// <summary>
    /// フィルタリング後のエントリー一覧。ListView の ItemsSource にバインドする。
    /// </summary>
    public ObservableCollection<AttendanceLogEntry> FilteredEntries { get; } = new();

    /// <summary>
    /// フィルタリング後の件数表示テキスト（例: "42 件"）。
    /// </summary>
    public ReactivePropertySlim<string> EntryCount { get; } = new("0 件");
}
