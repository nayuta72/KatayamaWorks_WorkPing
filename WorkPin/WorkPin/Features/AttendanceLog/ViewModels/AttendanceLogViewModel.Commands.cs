using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using WorkPin.Features.AttendanceLog.Models;
using WorkPin.Models; // AttendanceEntry, AttendanceAction

namespace WorkPin.Features.AttendanceLog.ViewModels;

/// <summary>
/// 出退勤ログページ ViewModel のコマンド定義とロジック。
/// </summary>
public partial class AttendanceLogViewModel
{
    // ===========================
    // コマンド定義
    // ===========================

    /// <summary>出勤：体調良好ボタン（笑顔）</summary>
    public ReactiveCommandSlim ClockInGoodCommand { get; private set; } = null!;

    /// <summary>出勤：いつもどおりボタン（真顔）</summary>
    public ReactiveCommandSlim ClockInNormalCommand { get; private set; } = null!;

    /// <summary>出勤：体調不良ボタン（辛そうな顔）</summary>
    public ReactiveCommandSlim ClockInBadCommand { get; private set; } = null!;

    /// <summary>退勤：順調ボタン（笑顔）</summary>
    public ReactiveCommandSlim ClockOutGoodCommand { get; private set; } = null!;

    /// <summary>退勤：計画どおりボタン（真顔）</summary>
    public ReactiveCommandSlim ClockOutNormalCommand { get; private set; } = null!;

    /// <summary>退勤：負荷高ボタン（辛そうな顔）</summary>
    public ReactiveCommandSlim ClockOutBadCommand { get; private set; } = null!;

    /// <summary>コメント入力ダイアログを開くコマンド</summary>
    public ReactiveCommandSlim OpenCommentDialogCommand { get; private set; } = null!;

    // コメント入力ダイアログを表示するためのイベント（View 側で購読する）
    public event Func<Task<string?>>? ShowCommentDialogRequested;

    /// <summary>コマンドを初期化する（コンストラクタから呼ばれる）。</summary>
    private void InitializeCommands()
    {
        ClockInGoodCommand    = new ReactiveCommandSlim().AddTo(Disposable);
        ClockInNormalCommand  = new ReactiveCommandSlim().AddTo(Disposable);
        ClockInBadCommand     = new ReactiveCommandSlim().AddTo(Disposable);
        ClockOutGoodCommand   = new ReactiveCommandSlim().AddTo(Disposable);
        ClockOutNormalCommand = new ReactiveCommandSlim().AddTo(Disposable);
        ClockOutBadCommand    = new ReactiveCommandSlim().AddTo(Disposable);
        OpenCommentDialogCommand = new ReactiveCommandSlim().AddTo(Disposable);

        // 各コマンドにロジックをバインドする
        ClockInGoodCommand.Subscribe(async _    => await RecordClockInAsync(AttendanceLogModel.StatusGood));
        ClockInNormalCommand.Subscribe(async _  => await RecordClockInAsync(AttendanceLogModel.StatusNormal));
        ClockInBadCommand.Subscribe(async _     => await RecordClockInAsync(AttendanceLogModel.StatusBad));
        ClockOutGoodCommand.Subscribe(async _   => await RecordClockOutAsync(AttendanceLogModel.StatusGood));
        ClockOutNormalCommand.Subscribe(async _  => await RecordClockOutAsync(AttendanceLogModel.StatusNormal));
        ClockOutBadCommand.Subscribe(async _    => await RecordClockOutAsync(AttendanceLogModel.StatusBad));
        OpenCommentDialogCommand.Subscribe(async _ => await HandleCommentAsync());
    }

    // ===========================
    // コマンドの実装ロジック
    // ===========================

    /// <summary>
    /// 出勤ステータスを記録する。
    /// settings.json と XML ログファイルの両方に書き込む。
    /// XML アクセスに失敗した場合は AccessCheckService のキューに保留する。
    /// </summary>
    private async Task RecordClockInAsync(string status)
    {
        var now      = DateTime.Now;
        var settings = _settingsService.Settings.Value;

        // 現在時刻と勤務形態を取得する
        var clockInTime = now.ToString("HH:mm");
        var workType    = IsRemoteWork.Value
            ? AttendanceLogModel.WorkTypeRemote
            : AttendanceLogModel.WorkTypeOffice;

        // ViewModel のプロパティを更新する（ボタンの色が変わる）
        ClockInStatus.Value = status;

        // settings.json に今日のステータスを保存する（再起動後の復元用）
        settings.InternalState.TodayClockInTime   = clockInTime;
        settings.InternalState.TodayClockInStatus = status;
        settings.InternalState.TodayWorkType      = workType;
        settings.InternalState.LastLogDate        = _model.GetTodayDateString();
        await _settingsService.SaveSettingsAsync();

        // XML ログファイルへの書き込みをキューに入れる
        // Action = ClockIn を指定することで LastLog には出勤関連属性のみ書き込まれる
        var entry = CreateEntry(settings, clockInTime, status, null, null, workType);
        entry.Action = AttendanceAction.ClockIn;
        await _accessCheckService.EnqueueWriteAsync(entry);
    }

    /// <summary>
    /// 退勤ステータスを記録する。
    /// </summary>
    private async Task RecordClockOutAsync(string status)
    {
        var now      = DateTime.Now;
        var settings = _settingsService.Settings.Value;

        var clockOutTime = now.ToString("HH:mm");
        var workType     = IsRemoteWork.Value
            ? AttendanceLogModel.WorkTypeRemote
            : AttendanceLogModel.WorkTypeOffice;

        // ViewModel のプロパティを更新する
        ClockOutStatus.Value = status;

        // settings.json に退勤ステータスを保存する
        settings.InternalState.TodayClockOutTime   = clockOutTime;
        settings.InternalState.TodayClockOutStatus = status;
        settings.InternalState.LastLogDate         = _model.GetTodayDateString();
        await _settingsService.SaveSettingsAsync();

        // XML ログファイルへの書き込みをキューに入れる
        // Action = ClockOut を指定することで LastLog には退勤関連属性のみ書き込まれる
        // （ログ本体エレメントには出勤情報も含めて記録する）
        var entry = CreateEntry(
            settings,
            settings.InternalState.TodayClockInTime,
            settings.InternalState.TodayClockInStatus,
            clockOutTime,
            status,
            workType);
        entry.Action = AttendanceAction.ClockOut;
        await _accessCheckService.EnqueueWriteAsync(entry);
    }

    /// <summary>
    /// コメント入力ダイアログを表示し、入力されたコメントを XML に書き込む。
    /// </summary>
    private async Task HandleCommentAsync()
    {
        // View 側のダイアログ表示処理を呼び出す
        if (ShowCommentDialogRequested == null) return;

        var comment = await ShowCommentDialogRequested.Invoke();
        if (comment == null) return; // キャンセルされた場合はスキップ

        var settings = _settingsService.Settings.Value;
        var workType = IsRemoteWork.Value
            ? AttendanceLogModel.WorkTypeRemote
            : AttendanceLogModel.WorkTypeOffice;

        // コメントを含むエントリーを書き込む
        // Action = Comment を指定することで LastLog には Name と Comment のみ書き込まれる
        var entry = CreateEntry(
            settings,
            settings.InternalState.TodayClockInTime,
            settings.InternalState.TodayClockInStatus,
            settings.InternalState.TodayClockOutTime,
            settings.InternalState.TodayClockOutStatus,
            workType,
            comment);
        entry.Action = AttendanceAction.Comment;

        await _accessCheckService.EnqueueWriteAsync(entry);
    }

    /// <summary>
    /// 出退勤エントリーオブジェクトを生成する。
    /// </summary>
    private AttendanceEntry CreateEntry(
        WorkPin.Models.AppSettings settings,
        string? clockInTime,
        string? clockInStatus,
        string? clockOutTime,
        string? clockOutStatus,
        string workType,
        string? comment = null)
    {
        return new AttendanceEntry
        {
            Date          = _model.GetTodayDateString(),
            UserId        = settings.UserId,
            Name          = settings.FullName,
            WorkType      = workType,
            ClockInTime   = clockInTime,
            ClockInStatus = clockInStatus,
            ClockOutTime  = clockOutTime,
            ClockOutStatus = clockOutStatus,
            Comment       = comment
        };
    }
}
