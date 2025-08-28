# PhotonBeat

プロジェクションマッピングMIDI音ゲー

## 概要

PhotonBeatは、MIDIキーボード（Impact LX88+など）を使用してUnity上で動作する音ゲーです。キーを押すと光の演出が縦に伸び、音が出ます。将来的にはプロジェクションマッピングによる空間投影を予定しています。

## セットアップ

### 必要環境
- Unity 6000.1.16f またはそれ以降
- MIDIキーボード（Impact LX88+ 推奨）
- Windows 10/11

### インストール手順

1. プロジェクトをクローンまたはダウンロード
2. Unityでプロジェクトを開く
3. Minisパッケージが自動的にインストールされるのを待つ
4. SampleSceneを開く
5. 空のGameObjectを作成し、`PhotonBeatSetup`スクリプトをアタッチ
6. Play を押してシステムを開始

### 使用方法

1. **自動セットアップ**：
   - `PhotonBeatSetup`コンポーネントの`Auto Setup`がチェックされていれば、再生時に自動的にシステムが構築されます

2. **手動セットアップ**：
   - ゲーム実行中にGUIの「Setup MIDI System」ボタンをクリック

3. **MIDIキーボード接続**：
   - MIDIキーボードをUSBで接続
   - Unityのコンソールで利用可能なデバイス一覧を確認
   - 必要に応じて`MidiInputHandler`の`Device Index`を調整

4. **テスト**：
   - GUIの「Test MIDI System」ボタンで動作確認
   - または実際にMIDIキーボードのキーを押して確認

## システム構成

### スクリプト

- **MidiInputHandler.cs**: MIDI入力の検出と処理
- **SimpleSynthesizer.cs**: リアルタイム音声合成
- **NoteVisualEffect.cs**: ノートの視覚効果システム
- **PhotonBeatSetup.cs**: システム全体のセットアップと管理

### 主な機能

- **MIDI入力検出**: Minisパッケージを使用したリアルタイムMIDI入力
- **音声合成**: ADSR エンベロープ付きシンセサイザー
- **ビジュアルエフェクト**: キー押下時の縦方向光演出
- **デバッグ機能**: コンソールログとランタイムGUI

## カスタマイズ

### MIDI設定
```csharp
// MidiInputHandlerで設定可能
midiSettings.lowestNote = 36;  // C2
midiSettings.highestNote = 96; // C7
midiSettings.minVelocity = 1;
```

### 音響設定
```csharp
// SimpleSynthesizerで設定可能
synthSettings.waveform = WaveformType.Sine;
synthSettings.masterVolume = 0.5f;
synthSettings.attack = 0.1f;
synthSettings.decay = 0.2f;
synthSettings.sustain = 0.7f;
synthSettings.release = 1f;
```

### ビジュアル設定
```csharp
// NoteVisualEffectで設定可能
visualSettings.totalWidth = 10f;
visualSettings.startHeight = 5f;
visualSettings.noteSpeed = 5f;
visualSettings.whiteKeyColor = Color.white;
visualSettings.blackKeyColor = Color.gray;
```

## 今後の予定

- [ ] より高度な視覚効果
- [ ] プロジェクションマッピング対応
- [ ] ノーツ判定システム
- [ ] スコアリングシステム
- [ ] 楽曲データの読み込み機能
- [ ] 網戸プロジェクション設定

## トラブルシューティング

### MIDI入力が検出されない
1. MIDIデバイスが正しく接続されているか確認
2. Unityコンソールでデバイス一覧を確認
3. `MidiInputHandler`の`Device Index`を調整

### 音が出ない
1. Unityの音量設定を確認
2. `SimpleSynthesizer`の`Master Volume`を確認
3. システム音量を確認

### ビジュアルエフェクトが表示されない
1. カメラの位置と向きを確認
2. `NoteVisualEffect`の設定を確認
3. シーンビューで確認