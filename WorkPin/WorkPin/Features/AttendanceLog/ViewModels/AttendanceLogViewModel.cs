using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WorkPin.Features.AttendanceLog.Models;
using WorkPin.Services;

namespace WorkPin.Features.AttendanceLog.ViewModels;

/// <summary>
/// 出退勤ログページの ViewModel（基本定義・コンストラクタ・Dispose）。
/// partial クラスとして分割：
///   - AttendanceLogViewModel.cs (このファイル)：基本定義・コンストラクタ
///   - AttendanceLogViewModel.Properties.cs：ReactiveProperty 定義
///   - AttendanceLogViewModel.Commands.cs：ReactiveCommand とロジック
/// </summary>
public partial class AttendanceLogViewModel : IDisposable
{
    // ===========================
    // 依存するサービスとモデル
    // ===========================
    private readonly SettingsService      _settingsService;
    private readonly AccessCheckService   _accessCheckService;
    private readonly WindowsLoginService  _windowsLoginService;
    private readonly NotificationService  _notificationService;
    private readonly AttendanceLogModel   _model;

    /// <summary>
    /// Dispose 対象をまとめた CompositeDisposable。
    /// すべての ReactiveProperty / ReactiveCommand を AddTo(Disposable) で登録する。
    /// </summary>
    public CompositeDisposable Disposable { get; } = new();

    public AttendanceLogViewModel(
        SettingsService      settingsService,
        AccessCheckService   accessCheckService,
        WindowsLoginService  windowsLoginService,
        NotificationService  notificationService)
    {
        _settingsService     = settingsService;
        _accessCheckService  = accessCheckService;
        _windowsLoginService = windowsLoginService;
        _notificationService = notificationService;
        _model               = new AttendanceLogModel();

        // プロパティとコマンドを初期化する
        InitializeProperties();
        InitializeCommands();
    }

    /// <summary>
    /// ページが表示されたときに呼ぶ初期化処理。
    /// View の Loaded イベントから呼ばれる（UIスレッド上で実行される）。
    /// </summary>
    public async Task InitializeAsync()
    {
        // UIスレッドの SynchronizationContext を取得する。
        // タイマーコールバックを UIスレッドに戻すために使う。
        // ※ InitializeAsync は Page_Loaded（UIスレッド）から呼ばれるため、ここで取得できる。
        var uiContext = SynchronizationContext.Current
                        ?? throw new InvalidOperationException("InitializeAsync は UIスレッドから呼んでください。");

        // 現在時刻の更新タイマーを開始する
        StartClockTimer(uiContext);

        // Windows ログイン時刻を取得して表示する（バックグラウンドで実行後、UIスレッドで反映）
        await LoadWindowsLoginTimesAsync();

        // 今日のステータスを settings から復元する
        RestoreTodayStatus();

        // 出勤ステータス未入力のリマインダーを開始する
        // ステータスが既に設定済みの場合は内部でスキップされる
        StartClockInReminderIfNeeded(uiContext);
    }

    /// <summary>
    /// 現在時刻を 1 秒ごとに更新するタイマーを開始する。
    /// Observable.Interval はスレッドプールで動作するため、
    /// ObserveOn で UIスレッドに戻してから Value をセットしないと
    /// WinUI 3 の XAML バインドがクロススレッド例外を起こす。
    /// </summary>
    private void StartClockTimer(SynchronizationContext uiContext)
    {
        System.Reactive.Linq.Observable
            .Interval(TimeSpan.FromSeconds(1))
            .ObserveOn(uiContext)                      // UIスレッドにマーシャルする
            .Subscribe(_ => CurrentTime.Value = DateTime.Now.ToString("HH:mm:ss"))
            .AddTo(Disposable);
    }

    /// <summary>
    /// Windows イベントログから昨日・今日のログイン時刻を非同期で取得する。
    /// 取得はバックグラウンドスレッドで行い、await 後（UIスレッド復帰後）に
    /// ReactiveProperty へ値をセットする。
    /// ※ Task.Run 内で Value をセットすると XAML バインドが
    ///   クロススレッド例外を起こすため、この順番を守ること。
    /// </summary>
    private async Task LoadWindowsLoginTimesAsync()
    {
        // 今日のログイン時刻：explorer.exe から即時取得（管理者権限不要）
        var todayLogin = await Task.Run(() => _windowsLoginService.GetTodayFirstLoginTime());

        // 前回起動日のログイン/ログアウト時刻：System イベントログから取得
        // Task.WhenAny で 5 秒タイムアウト管理済みのため UI はブロックされない
        var (prevLogin, prevLogout) =
            await _windowsLoginService.GetPreviousBootDayTimesAsync();

        // await 後は UIスレッドに戻っているため、ここで ReactiveProperty に値をセットする
        // 今日のログインは時刻のみ、前回起動は日付も表示する（前回が今日かどうか不明なため）
        TodayLoginTime.Value      = todayLogin?.ToString("HH:mm")            ?? "取得できませんでした";
        YesterdayLoginTime.Value  = prevLogin?.ToString("yyyy/MM/dd HH:mm")  ?? "取得できませんでした";
        YesterdayLogoutTime.Value = prevLogout?.ToString("yyyy/MM/dd HH:mm") ?? "取得できませんでした";
    }

    /// <summary>
    /// 出勤ステータス未入力のリマインダーを開始する。
    ///
    /// 動作：
    ///   ・起動から 5 分後に最初の通知を送る。
    ///   ・その後は 10 分ごとに通知を繰り返す。
    ///   ・出勤ステータスが入力された瞬間（ClockInStatus が非 null になった瞬間）に
    ///     TakeUntil によって購読が自動的に停止される。
    ///   ・既にステータスが設定済みの場合（ツール再起動後の復元時など）は何もしない。
    /// </summary>
    private void StartClockInReminderIfNeeded(SynchronizationContext uiContext)
    {
        // 今日の出勤ステータスが既に入力済みなら通知不要
        if (ClockInStatus.Value != null) return;

        // Observable.Timer(5分) … 起動後5分で最初の通知
        // Concat(Interval(10分)) … その後10分ごとに繰り返す
        // TakeUntil … ClockInStatus に値が入ったら自動停止
        Observable
            .Timer(TimeSpan.FromMinutes(5))
            .Concat(Observable.Interval(TimeSpan.FromMinutes(10)))
            .ObserveOn(uiContext)
            .TakeUntil(ClockInStatus.Where(s => s != null))
            .Subscribe(_ =>
            {
                // 念のため二重チェック（TakeUntil との競合を避ける）
                if (ClockInStatus.Value != null) return;

                _notificationService.ShowNotification(
                    "WorkPin",
                    "出勤ステータスが未入力です。ボタンを押してください。");
            })
            .AddTo(Disposable);
    }

    /// <summary>
    /// settings.json に保存されている今日のステータスを復元する。
    /// 日付が変わっていた場合はステータスをリセットする。
    /// </summary>
    private void RestoreTodayStatus()
    {
        var s = _settingsService.Settings.Value;

        // 保存されている日付が今日のものでない場合はリセットする
        if (!_model.IsTodayLog(s.InternalState.LastLogDate))
        {
            // 日付が変わったのでステータスをリセット（colors のみ、settings の書き込みは次回保存時）
            ClockInStatus.Value  = null;
            ClockOutStatus.Value = null;
            return;
        }

        // 今日の出勤・退勤ステータスを復元する
        ClockInStatus.Value  = s.InternalState.TodayClockInStatus;
        ClockOutStatus.Value = s.InternalState.TodayClockOutStatus;

        // 勤務形態を復元する
        IsRemoteWork.Value = s.InternalState.TodayWorkType == AttendanceLogModel.WorkTypeRemote;
    }

    public void Dispose()
    {
        Disposable.Dispose();
    }
}
