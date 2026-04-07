// 自動生成の Main()（App.g.i.cs）を DISABLE_XAML_GENERATED_MAIN で無効化し、
// このファイルのカスタム Main() をエントリーポイントとして使用する。
//
// 【なぜ必要か】
// 自己完結型（SelfContained）の WinUI 3 アンパッケージドアプリでは、
// 自動生成された Main() が COM アクティベーションを確実に完了できず、
// OnLaunched() が呼ばれないままプロセスが終了することがある。
// カスタム Main() で明示的に初期化することでこの問題を回避する。

namespace WorkPing;

/// <summary>
/// アプリケーションのエントリーポイント。
/// 自動生成版と同じ手順を明示的に記述し、起動の安定性を確保する。
/// </summary>
public static class Program
{
    [global::System.STAThreadAttribute]
    static void Main(string[] args)
    {
        // WinRT の COM ラッパーサポートを初期化する（Application.Start() の前に必須）
        global::WinRT.ComWrappersSupport.InitializeComWrappers();

        // WinUI 3 アプリケーションの起動ループを開始する。
        // コールバック内で App インスタンスを生成し、その後 OnLaunched() が呼ばれる。
        // 全ウィンドウが閉じられるまでこのメソッドはブロックし続ける。
        global::Microsoft.UI.Xaml.Application.Start((p) =>
        {
            // DispatcherQueue の同期コンテキストを設定する。
            // async/await が UI スレッドに正しく戻れるようにするために必要。
            var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);

            new App();
        });
    }
}
