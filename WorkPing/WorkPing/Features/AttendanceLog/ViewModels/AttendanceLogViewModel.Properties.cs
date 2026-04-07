using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Reactive.Linq;

namespace WorkPing.Features.AttendanceLog.ViewModels;

/// <summary>
/// 出退勤ログページ ViewModel の ReactiveProperty 定義。
/// </summary>
public partial class AttendanceLogViewModel
{
    // ===========================
    // Windows ログイン時刻表示
    // ===========================

    /// <summary>昨日の最初のログイン時刻（HH:mm 形式）</summary>
    public ReactivePropertySlim<string> YesterdayLoginTime { get; private set; } = null!;

    /// <summary>昨日の最後のログアウト時刻（HH:mm 形式）</summary>
    public ReactivePropertySlim<string> YesterdayLogoutTime { get; private set; } = null!;

    /// <summary>今日の最初のログイン時刻（HH:mm 形式）</summary>
    public ReactivePropertySlim<string> TodayLoginTime { get; private set; } = null!;

    /// <summary>
    /// 現在時刻（HH:mm:ss 形式）。
    /// 1 秒ごとにリアルタイムで更新される。
    /// </summary>
    public ReactivePropertySlim<string> CurrentTime { get; private set; } = null!;

    // ===========================
    // 勤務形態トグル
    // ===========================

    /// <summary>
    /// 在宅勤務フラグ。
    /// true = 在宅、false = 出社。
    /// XAML の ToggleSwitch に双方向バインドする。
    /// </summary>
    public ReactivePropertySlim<bool> IsRemoteWork { get; private set; } = null!;

    // ===========================
    // 出勤・退勤ステータス
    // ===========================

    /// <summary>
    /// 今日の出勤ステータス（◯/△/✕/null）。
    /// null = 未登録、ボタンの色変えに使用する。
    /// </summary>
    public ReactivePropertySlim<string?> ClockInStatus { get; private set; } = null!;

    /// <summary>今日の退勤ステータス（◯/△/✕/null）。</summary>
    public ReactivePropertySlim<string?> ClockOutStatus { get; private set; } = null!;

    // ===========================
    // 計算プロパティ（ReadOnly）
    // ===========================

    /// <summary>出勤ボタン（良い）が選択中かどうか</summary>
    public ReadOnlyReactivePropertySlim<bool> IsClockInGood { get; private set; } = null!;

    /// <summary>出勤ボタン（普通）が選択中かどうか</summary>
    public ReadOnlyReactivePropertySlim<bool> IsClockInNormal { get; private set; } = null!;

    /// <summary>出勤ボタン（悪い）が選択中かどうか</summary>
    public ReadOnlyReactivePropertySlim<bool> IsClockInBad { get; private set; } = null!;

    /// <summary>退勤ボタン（良い）が選択中かどうか</summary>
    public ReadOnlyReactivePropertySlim<bool> IsClockOutGood { get; private set; } = null!;

    /// <summary>退勤ボタン（普通）が選択中かどうか</summary>
    public ReadOnlyReactivePropertySlim<bool> IsClockOutNormal { get; private set; } = null!;

    /// <summary>退勤ボタン（悪い）が選択中かどうか</summary>
    public ReadOnlyReactivePropertySlim<bool> IsClockOutBad { get; private set; } = null!;

    /// <summary>プロパティを初期化する（コンストラクタから呼ばれる）。</summary>
    private void InitializeProperties()
    {
        // ログイン時刻（初期値は取得中を示す文字列）
        YesterdayLoginTime  = new ReactivePropertySlim<string>("取得中...").AddTo(Disposable);
        YesterdayLogoutTime = new ReactivePropertySlim<string>("取得中...").AddTo(Disposable);
        TodayLoginTime      = new ReactivePropertySlim<string>("取得中...").AddTo(Disposable);
        CurrentTime         = new ReactivePropertySlim<string>(DateTime.Now.ToString("HH:mm:ss")).AddTo(Disposable);

        // トグルスイッチ
        IsRemoteWork = new ReactivePropertySlim<bool>(false).AddTo(Disposable);

        // ステータス
        ClockInStatus  = new ReactivePropertySlim<string?>(null).AddTo(Disposable);
        ClockOutStatus = new ReactivePropertySlim<string?>(null).AddTo(Disposable);

        // 出勤ステータスから各ボタンの「選択中」状態を計算する
        IsClockInGood   = ClockInStatus.Select(s => s == Models.AttendanceLogModel.StatusGood)
                            .ToReadOnlyReactivePropertySlim().AddTo(Disposable);
        IsClockInNormal = ClockInStatus.Select(s => s == Models.AttendanceLogModel.StatusNormal)
                            .ToReadOnlyReactivePropertySlim().AddTo(Disposable);
        IsClockInBad    = ClockInStatus.Select(s => s == Models.AttendanceLogModel.StatusBad)
                            .ToReadOnlyReactivePropertySlim().AddTo(Disposable);

        // 退勤ステータスから各ボタンの「選択中」状態を計算する
        IsClockOutGood   = ClockOutStatus.Select(s => s == Models.AttendanceLogModel.StatusGood)
                             .ToReadOnlyReactivePropertySlim().AddTo(Disposable);
        IsClockOutNormal = ClockOutStatus.Select(s => s == Models.AttendanceLogModel.StatusNormal)
                             .ToReadOnlyReactivePropertySlim().AddTo(Disposable);
        IsClockOutBad    = ClockOutStatus.Select(s => s == Models.AttendanceLogModel.StatusBad)
                             .ToReadOnlyReactivePropertySlim().AddTo(Disposable);
    }
}
