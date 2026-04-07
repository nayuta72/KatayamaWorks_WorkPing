# Project: WorkPing (WinUI3 + .NET 10 + ReactiveProperty)
- 開発するアプリケーションはWindows 11の64bit OSでしか使わないので32bit版などのことは考慮する必要なし。

## 操作確認について
- 操作の確認や進捗報告は、できる限り日本語で行ってください。ツール実行時のプロンプトが英語でも、その説明は日本語で添えてください。

## プロジェクト構成ルール
- **機能単位のフォルダ構成 (Feature-based Folder Structure):**
  - 各機能ごとに `Features/[FeatureName]/` フォルダを作成する。
  - 内部に `Views`, `ViewModels`, `Models` フォルダを配置する。
- **命名規則:**
  - 役割が明確な命名にする（例: `MainPage.xaml`, `MainViewModel.cs`, `MainModel.cs`）。
- **UI構成方針:**
  - MainWindow 上に各機能の UserControl を配置して画面を構成する。
  - 共通のUIコンポーネントのみ `Common/Controls/` に配置する。
  - ウィンドウおよびサブウィンドウはAcrylic（背景ブラー）にする。

## コード分割ルール (Partial Class Split)
- **ViewModel / Model の分割:**
  - `partial` を使用して役割ごとにファイルを分割する。
  - `[Name].cs`: 基本定義、コンストラクタ、DIによるサービス受け取り、`CompositeDisposable` によるDispose処理。
  - `[Name].Properties.cs`: `ReactiveProperty` 定義。
  - `[Name].Commands.cs`: `ReactiveCommand` やロジック。

## データ管理・DI ルール
- **依存性の注入 (Dependency Injection):**
  - インスタンスの共有には Singleton ではなく、**Microsoft.Extensions.DependencyInjection** を使用する。
  - すべての Service と ViewModel は `App.xaml.cs` で DI コンテナに登録する。
  - ViewModel はコンストラクタ注入 (Constructor Injection) で必要なサービスを受け取る。
- **共有データ:**
  - サービス内で `ReactivePropertySlim<T>` を使用して状態を保持し、変更通知を行う。

## 技術スタック
- **言語** C# 13
- **Framework:** .NET 10 / WinUI 3 (Unpackaged)
- **Reactive Library:** ReactiveProperty v9.8.0 (Reactive.Bindings)
- **MVVM:** ReactiveProperty (CommunityToolkit.Mvvm は使用せず ReactiveProperty で統一)
- **注意点** Winui3のビルドのためにVisualStudio2026をインストールしたパソコンで開発するが、VisualStudio2022と.NET10の開発環境でもデバッグなどできるようにしておくこと。作成したソリューション作成がVisualStudio2022で開けないのは困る。

- **ReactiveProperty(v9.8.0)の実装指針:**
  - 名前空間は `using Reactive.Bindings;` および `using Reactive.Bindings.Extensions;` を使用する。
  - ViewModelには `CompositeDisposable Disposable = new();` を用意し、プロパティやコマンドの生成時に `.AddTo(this.Disposable)` で登録すること。
  - `IDisposable` インターフェースを実装し、`Dispose()` 内で `Disposable.Dispose()` を呼び出すコードを必ず含めること。
  - **Slim版を優先して使用する（パフォーマンスが良く、バリデーション不要な場合はSlim）:**
    - プロパティ: `ReactivePropertySlim<T>` を基本とし、データアノテーションバリデーションが必要な場合のみ `ReactiveProperty<T>` を使用する。
    - 読み取り専用プロパティ: `ReadOnlyReactivePropertySlim<T>` を基本とし、`.ToReadOnlyReactivePropertySlim()` で生成する。
    - コマンド: `ReactiveCommandSlim` を基本とし、スレッド制御・リトライ等が必要な場合のみ `ReactiveCommand` を使用する。
    - canExecute ソースからコマンド生成: `canExecuteObservable.ToReactiveCommandSlim()` を使用する。

## コーディング規約
- **言語:** 回答およびコード内のコメントはすべて **日本語**。
- **命名:** メソッド・クラスは `PascalCase`、非同期には `Async` を付ける。
- **コメントについて** コメントは、プロジェクトに後から入ってきた新人がすぐに理解できるように少し多めかつわかりやすく丁寧(親切)に記載する。

## 開発コマンド
- **ビルド:** `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WorkPing/WorkPing/WorkPing.csproj -p:Platform=x64 -p:Configuration=Debug`
  - `dotnet build` は WinUI3 では使用不可（PRI生成タスクがVSのDLLに依存するため）
- **実行:** ビルド後 `C:\tmp\WorkPing\bin\WorkPing\x64\Debug\net10.0-windows10.0.19041.0\WorkPing.exe` を直接実行（OneDrive管理外の C:\tmp\ に出力）
  - または Visual Studio 2022 / 2026 からデバッグ実行

## Git 運用ルール

- **コミットメッセージ:** 日本語で記載し、プレフィックス（例: feat:, fix:, docs:）を付ける。

- **コミット単位:** 機能実装ごと、または大きなリファクタリングごとにこまめにコミットする。
- **ブランチ**: 新機能の開発時は feature/[機能名] ブランチを作成して作業する。