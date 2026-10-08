# SDR# 空線信号キャンセラー

約1.5 kHzの空線信号を検出し、検出中の音声をミュートするWindows版SDR#プラグインの開発リポジトリです。

必要な機能:

1. 約1.5 kHzの空線信号をキャンセルする。
2. 空線信号を検出中は受信音全体をミュートする。
3. 空線信号の存在を文字と色で表示する。
4. 利用者の操作でいつでもキャンセラーを停止し、音声を通過させる。

現時点は調査と設計の段階です。プラグインDLLはまだ作成していません。

- 開発ルール: [AGENTS.md](AGENTS.md)
- 参照資料と測定結果: [docs/reference-findings.md](docs/reference-findings.md)
- 設計案: [docs/superpowers/specs/2026-10-09-idle-signal-canceller-design.md](docs/superpowers/specs/2026-10-09-idle-signal-canceller-design.md)

録音やIQデータ、SDR#本体のDLLをこのリポジトリへコピーしません。
