# Advanced Analog Clock

Windows デスクトップ向けのアナログ時計アプリです。  
WPF (.NET 8) で実装しており、Outlook デスクトップから当日の予定を取得して、時計の文字盤に重ねて表示できます。

## 主な機能

- アナログ時計表示
	- 秒目盛り・1 から 12 の数字表示
	- ボーダーレス、常に最前面、ドラッグ移動
	- 正方形の縦横比を維持したリサイズ
- テーマ切り替え
	- コンテキストメニューからライト/ダーク切り替え
- Outlook 予定の可視化
	- コンテキストメニューから手動で予定取得
	- 取得した予定を文字盤上に重ねて表示（終日予定は文字盤表示対象外）
	- 重なり予定は最大 4 レーンで表示
	- 時針位置に現在時刻の目印を表示
- 予定一覧と詳細
	- コンテキストメニューの「予定一覧」に当日予定を表示（終日予定含む）
	- 過去予定は背景グレー、現在進行中の時間指定予定はハイライト
	- 文字盤の予定帯をクリックすると詳細ウィンドウ表示
	- 会議リンクが見つかる場合は「参加リンクを開く」が有効

## 動作要件

- Windows
- .NET 8 SDK または Runtime
- Outlook デスクトップ（予定取得機能を使う場合）

## 起動方法

### 1) 直接起動

```powershell
dotnet run --project .\src\AdvancedAnalogClock\src\AdvancedAnalogClock.App\AdvancedAnalogClock.App.csproj
```

### 2) バッチ起動

```powershell
startApp.bat
```

## 使い方（予定取得）

1. アプリを起動
2. 時計を右クリック
3. 「予定を取得」をクリック
4. 「予定一覧」で取得結果を確認

注意: 起動直後に自動取得はしません。Outlook が未起動のタイミングで誤作動しないための仕様です。

## ビルド

```powershell
dotnet build .\src\AdvancedAnalogClock\AdvancedAnalogClock.sln
```

## 配布用 EXE 作成（単一ファイル）

```powershell
dotnet publish .\src\AdvancedAnalogClock\src\AdvancedAnalogClock.App\AdvancedAnalogClock.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:UseAppHost=true -p:DebugType=None -p:DebugSymbols=false -o .\publish
```

または:

```powershell
publishApp.bat
```

出力ファイル名は `AdvancedAnalogClock.exe` です。

## スタートアップ登録

```powershell
copyToStartUpDir.bat
```

上記は `publish\AdvancedAnalogClock.exe` をユーザーのスタートアップフォルダへコピーします。

## 補足

- Outlook 予定取得はローカルの Outlook COM を利用しています。
- 予定本文が非常に長い場合、Outlook 側の取得結果に依存して表示されます。
- 配布先で予定機能を使う場合も、Outlook デスクトップが必要です。