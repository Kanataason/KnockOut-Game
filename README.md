# KnockOut

3Dモデルを使った、1対1の対戦格闘ゲームです。
フレーム単位で動く格闘ゲームの仕組み（固定ティック、フレームデータ、ヒットストップ、投げ抜けなど）を自作しています。

<!-- プレイ動画・スクリーンショット -->
<!-- ![プレイ画面](Docs/screenshot.png) -->
【プレイ動画のURLを記入】

## 概要

| 項目 | 内容 |
|---|---|
| ジャンル | 1対1 対戦格闘ゲーム |
| プレイ人数 | 2人（ゲームパッド2台、またはキーボード1台で2人） |
| キャラクター | GOLIATH、RADAR |
| エンジン | Unity 6000.3.10f1（URP） |
| 言語 | C# |
| 主なパッケージ | Input System、Addressables、Timeline、TextMesh Pro |
| 開発期間 | 【記入】 |
| 開発人数 | 【記入（担当した範囲も）】 |

## 遊び方

ビルド済みの実行ファイルを [Releases](../../releases) に置いています。【ビルドを置いたら記入】

> **注意：** 再配布が許可されていない素材（キャラクターモデル、アニメーション、一部の背景など）は、このリポジトリに含めていません。
> そのため、このリポジトリを Unity で開くと、モデルやアニメーションが表示されません。遊ぶときはビルドを使ってください。
> 詳しくは [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を見てください。

### 操作方法（キーボード）

| 操作 | キー |
|---|---|
| 移動 | A / D |
| ジャンプ | W |
| しゃがみ | S |
| 弱攻撃 | U |
| 中攻撃 | I |
| 強攻撃 | O |
| 特殊攻撃 | J |
| 必殺技（ゲージ3本） | K |
| ティルト攻撃 | P |
| 投げ | H |
| ポーズ / 戻る | Esc |
| 決定 | Space |

ゲームパッドにも対応しています。

## ゲームシステム

- ラウンド制（制限時間99秒）。KO か、時間切れの時点で体力が多い方の勝ち
- 攻撃を当てる・ガードするとゲージがたまり、3本で必殺技が使える（必殺技はカメラのズーム演出つき）
- 投げと投げ抜け（受付時間内に投げを入力すると抜けられる）
- 空中コンボは回数に上限があり、上限を超えた攻撃は当たらない（必殺技は除く）
- キャラクターごとの固有の技（GOLIATH のパワーアップ、RADAR の飛び道具）

## 技術的なポイント

### 1. 固定ティック（1秒60回）で動くゲームロジック
描画のフレームレートに関係なく、ゲームの処理を必ず1秒に60回進めています。技のフレーム数がPCの性能で変わらないようにするためです。

- [BattleTicker](Assets/Script/ManagerScript/BattleTicker.cs)：経過時間をためて、1/60秒ごとに登録された処理を呼ぶ。フレーム時間の小さなぶれは吸収し、60fps では毎フレームちょうど1ティック進める
- [ITickable](Assets/Script/GenericScript/ITickable.cs)：ティックで動かすクラスが実装するインターフェース
- [TickPhase / BattleTickOrder](Assets/Script/DataScript/BattleTickOrder.cs)：1ティックの中の処理順（状態 → 行動 → ノックバック → 移動 → 弾 → 押し出し → 試合の進行）。同じ段階では必ず Player1 → Player2 の順に処理する
- [TickScheduler](Assets/Script/GenericScript/TickScheduler.cs)：「〇ティック後に実行する」予約。コルーチンを使わずティックで数えるので、ポーズ中は待ち時間も止まる

### 2. ScriptableObject で持つフレームデータ
技ごとの発生・持続・硬直、ヒットボックスを出すフレーム、ダメージ、ヒットストップ、ゲージの増加量などを [ActionFrameData](Assets/Script/DataScript/ActionFrameData.cs) にまとめています。コードを変えずに、データだけで技を調整できます。
[AttackFrameController](Assets/Script/ManagerScript/AttackFrameController.cs) がティックごとにフレームデータを読み、当たり判定を出し入れします。

### 3. ステートマシンによるプレイヤーの行動管理
汎用の [StateMachine\<T\>](Assets/Script/GenericScript/StateMachine.cs) を作り、[PlayerController](Assets/Script/PlayerScript/PlayerController.cs) の行動（待機・移動、ジャンプ、しゃがみ、ガード、攻撃、ステップ、やられ、行動不能、死亡、勝利）をステートで管理しています。

### 4. Playable API によるアニメーション
[PlayableAnimationPlayer](Assets/Script/PlayerScript/PlayableAnimationPlayer.cs) で、AnimatorController（待機や移動）と攻撃のクリップを LayerMixer で重ねています。攻撃のアニメーションは、ティックごとにフレームを指定して再生するので、当たり判定とずれません。

### 5. 試合の進行と演出の分離
[BattleStateManager](Assets/Script/ManagerScript/BattleStateManager.cs) が試合の流れ（ラウンド開始 → 試合中 → KO / 時間切れ → 勝者の表示 → 次のラウンド）を決め、UI（[UiTextMoveManager](Assets/Script/UiScript/UiTextMoveManager.cs)）は演出を再生して終わったら戻るだけにしています。

### 6. 理由ごとに管理するポーズ
[PauseService](Assets/Script/GenericScript/UiUtility.cs) で「ポーズメニュー」「KOのズーム演出」など、時間を止める理由をまとめて管理しています。理由が1つでも残っていれば止まったままなので、ズーム中にポーズを閉じても動き出しません。

### 7. その他
- 攻撃データ、エフェクト、音声を Addressables で非同期に読み込み
- エフェクトはオブジェクトプールで使い回し（[ObjectPool](Assets/Script/GenericScript/ObjectPool.cs)）
- Input System で、ゲームパッド2台、キーボード1台で2人、パッドとキーボードの組み合わせに対応

## フォルダ構成

```
Assets/Script
├── CharacterScript   キャラクター固有の技（GOLIATH、RADAR）
├── DataScript        ScriptableObject のデータ、プレイヤー番号、処理順
├── GenericScript     ステートマシン、ティックの予約、オブジェクトプールなどの汎用処理
├── InputScript       入力デバイスとプレイヤーの紐づけ
├── ManagerScript     試合の進行、ティック、プレイヤーの生成、UIイベント
├── OtherScript       カメラ、シーンの読み込みなど
├── PlayerScript      プレイヤーの行動、アニメーション、当たり判定、ダメージ処理
└── UiScript          HUD、メニュー、文字演出
```

## 今後の課題

自分で把握している改善点です。

- `PlayerController` が約1900行あり、各ステートを別ファイルに分けたい
- プレイヤーの部品（PlayerController、AnimationManager、AttackFrameController など）がお互いを直接参照しているので、依存の向きを一方向に整理したい
- シングルトンへの依存が多いので、シーン内でだけ使うマネージャーは参照を渡す形にしたい
- 一部の名前に綴りの間違いが残っている（`MidlleAttack` など）

## ライセンス

`Assets/Script` のソースコードは MIT License です。それ以外の素材は対象外です。詳しくは [LICENSE](LICENSE) と [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を見てください。
