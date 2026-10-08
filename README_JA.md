# SDR# 空線信号キャンセラー 1.0.0

約1.5 kHzの空線トーンを検出し、その間の受信音全体をミュートします。
「空線信号あり／ミュート中」を文字と緑色で表示し、チェックを外すと停止します。

## 対応環境

Windowsの.NET 9版SDR#、NFM用です。SDR# 1921の実アセンブリでビルドと連携テストを実施しています。
AnyCPUで独自のネイティブDLLはなく、x64で検証しています。x86の実行は未検証です。
旧.NET Framework版と.NET 8専用ホストは対象外です。NFM以外は音声を通過させます。

## インストール

1. SDR#を終了します。
2. 配布ZIP内のPluginsフォルダを、SDR#の導入先へコピーします。
3. 既存のPluginsへ次の2ファイルを追加します。他のファイルは残してください。

   ```text
   Plugins/SDRSharp.IdleSignalCanceller.dll
   Plugins/SDRSharp.IdleSignalCanceller.Core.dll
   ```

4. SDR#を起動し、「空線信号キャンセラー」パネルを開きます。
5. NFMで受信を開始し、「キャンセラーを有効にする」をチェックします。

Plugins.xmlへの追記は不要です。配置先を本体設定で変更している場合は、そのフォルダへ配置します。
毎回の起動時は無効です。設定はこの版では保存しません。

## 調整と停止

| 設定 | 初期値 | 調整方法 |
| --- | ---: | --- |
| 中心周波数 | 1500 Hz | 実際のトーンに合わせます。RF周波数とは別です。 |
| 許容幅 | ±50 Hz | 周波数のずれが大きい場合に広げます。窓の分解能による境界の幅があります。 |
| 検出閾値 | 7 dB | 小さいほど高感度。通話を誤ミュートする場合は上げます。 |
| 検出継続 | 160 ms | 短い音を誤認する場合は長くします。 |
| 解除時間 | 80 ms | 通話冒頭が消える場合は短く、表示の点滅が多い場合は長くします。 |
| 最小レベル | -65 dBFS | 音声フック入力の下限です。本体のゲイン設定にも影響されます。 |

検出は40 ms単位です。ミュート開始まで初期値で約160 msと最大約5 msのフェードがあり、最初の短いトーンは聞こえます。
トーン終了後の解除にも通常80～120 ms程度を要し、通話冒頭を短く抑制する可能性があります。
継続・解除時間は40 ms単位へ切り上げて処理します。
弱い受信では表示するトーン突出度を見ながら閾値を下げ、通話が通過することも確認してください。
極端に高感度にすると雑音や声を誤認することがあります。

チェックを外すと、次の処理バッファから無加工で通過させます。既に音声デバイスへ渡ったバッファの遅延は残ります。
本体のスケルチ、音量、ミュート設定は変更しません。他のプラグインや本体が音を止めている場合は、その状態に従います。
選局変更や受信停止・再開では検出履歴をクリアします。受信停止、NFM以外、対応外のサンプルレートは表示で知らせます。

## ビルドとテスト

.NET 9対応SDK、Python 3.10以上、実際のSDR#本体または開発用DLLが必要です。
外部NuGetパッケージは使いません。リポジトリのルートからPowerShellで実行します。

```powershell
./tools/build.ps1 -SdrSharpPath 'C:/path/to/SDRSharp'
```

単一EXEも指定できます。Common/Radio/PanViewをGitから除外されたvendor/referencesへ抽出します。
DLLが分離してある場合は、そのディレクトリを指定します。
ビルドと全テストが成功すると、artifacts/SDRSharp.IdleSignalCanceller-1.0.0.zipを作成します。
ZIPにはプラグインの2つのDLL、導入説明、匿名化した検証資料を含めます。

```powershell
dotnet run --project tests/IdleSignal.Tests -c Release
dotnet run --project tools/IdleSignal.Evaluate -c Release -- 'C:/path/to/audio.wav' artifacts/evaluation.json
```

評価ツールはmono/stereoのPCM16/float32 WAVに対応し、原ファイル名、パス、受信条件を出力JSONへ記録しません。

GitHub ActionsはLinuxでコアを検証し、WindowsでAirspy配布物から実参照を取得して全体をビルド・テストします。
成功すると配布ZIPをActionsのArtifactsへ出力します。配布元の可用性に依存します。
pushは行っていないため、GitHub上のActions実行はまだ未確認です。

## 検証範囲と削除

人工信号、実際のSDR#音声フック、日本語UI、提供録音によるオフライン評価を実施しています。
録音には通話の正解ラベルがないため、ミュート時間率は通話認識精度を意味しません。
録音、IQ、本体DLL、個人パス、受信条件はGitや配布物へ含めません。
検証の詳細は[docs/validation.md](docs/validation.md)、匿名化した解析結果は[docs/reference-findings.md](docs/reference-findings.md)を参照してください。

削除する場合はSDR#を終了し、当プラグインの2つのDLLだけを取り除きます。
