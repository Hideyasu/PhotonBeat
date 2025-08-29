using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using RtMidi;

public class SimpleMidiTestGX61 : MonoBehaviour
{
    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float volume = 0.5f;
    
    [Range(0.01f, 2f)]
    public float attackTime = 0.01f;
    
    [Range(0.1f, 3f)]
    public float releaseTime = 0.5f;
    
    private Dictionary<int, AudioSource> activeSources = new Dictionary<int, AudioSource>();
    private Dictionary<int, AudioClip> preGeneratedClips = new Dictionary<int, AudioClip>();
    private int sampleRate = 44100;
    private MidiIn midiIn;
    private bool isGX61Connected = false;
    
    // ビジュアライザー
    private KeyboardVisualizer visualizer;
    
    // ノートゲーム
    private MidiNoteGame noteGame;
    
    // MIDIメッセージをメインスレッドで処理するためのキュー
    private struct MidiEvent
    {
        public byte messageType;
        public byte note;
        public byte velocity;
        public byte channel;
    }
    private ConcurrentQueue<MidiEvent> midiEventQueue = new ConcurrentQueue<MidiEvent>();
    
    void Awake()
    {
        // 最速で初期化
        Application.targetFrameRate = 120; // フレームレート上限を上げる
    }
    
    void Start()
    {
        Debug.Log("=== SimpleMidiTestGX61 起動 ===");
        
        // AudioListenerの確認
        var listener = FindObjectOfType<AudioListener>();
        if (listener == null)
        {
            gameObject.AddComponent<AudioListener>();
            Debug.Log("AudioListenerを自動追加しました。");
        }
        
        // オーディオ設定を低遅延に最適化
        var audioConfig = AudioSettings.GetConfiguration();
        audioConfig.dspBufferSize = 256; // バッファサイズを小さく（デフォルト1024）
        audioConfig.sampleRate = 48000; // 一般的な低遅延サンプルレート
        audioConfig.speakerMode = AudioSpeakerMode.Stereo;
        AudioSettings.Reset(audioConfig);
        
        sampleRate = AudioSettings.outputSampleRate;
        AudioListener.volume = 1.0f;
        
        Debug.Log($"低遅延オーディオ設定:");
        Debug.Log($"- DSPバッファ: {audioConfig.dspBufferSize}");
        Debug.Log($"- サンプルレート: {sampleRate}Hz");
        
        // ビジュアライザーを探すか作成
        visualizer = FindObjectOfType<KeyboardVisualizer>();
        if (visualizer == null)
        {
            GameObject visualizerObject = new GameObject("KeyboardVisualizer");
            visualizer = visualizerObject.AddComponent<KeyboardVisualizer>();
            Debug.Log("KeyboardVisualizerを作成しました");
        }
        
        // ノートゲームの参照を取得
        noteGame = FindObjectOfType<MidiNoteGame>();
        
        // よく使用される音階のAudioClipを事前生成（全音域）
        StartCoroutine(PreGenerateAudioClips());
        
        InitializeRtMidi();
    }
    
    IEnumerator PreGenerateAudioClips()
    {
        Debug.Log("AudioClip事前生成開始...");
        
        // 全MIDI音域（0-127）を事前生成
        for (int note = 0; note < 128; note++)
        {
            float frequency = 440f * Mathf.Pow(2f, (note - 69f) / 12f);
            
            // 短めのクリップ（1秒）でループ再生用
            int sampleLength = Mathf.FloorToInt(sampleRate * 1f);
            AudioClip clip = AudioClip.Create($"PreGen_Note_{note}", sampleLength, 1, sampleRate, false);
            float[] samples = new float[sampleLength];
            
            // 極短アタック（1ms）
            float attackSamples = sampleRate * 0.001f;
            
            for (int i = 0; i < sampleLength; i++)
            {
                float time = (float)i / sampleRate;
                float envelope = i < attackSamples ? (float)i / attackSamples : 1f;
                
                // よりリッチな音色（基音＋倍音）
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.25f;
                samples[i] += Mathf.Sin(4f * Mathf.PI * frequency * time) * envelope * 0.1f;
                samples[i] += Mathf.Sin(8f * Mathf.PI * frequency * time) * envelope * 0.05f;
            }
            
            clip.SetData(samples, 0);
            preGeneratedClips[note] = clip;
            
            // 8音ごとに1フレーム待つ（高速化）
            if (note % 8 == 0) yield return null;
        }
        
        Debug.Log($"AudioClip事前生成完了: 全{preGeneratedClips.Count}音");
    }
    
    void InitializeRtMidi()
    {
        Debug.Log("=== RtMidi 初期化開始 ===");
        
        try
        {
            midiIn = MidiIn.Create();
            
            if (midiIn == null)
            {
                Debug.LogError("MidiIn の作成に失敗しました");
                return;
            }
            
            var portCount = midiIn.PortCount;
            Debug.Log($"検出されたMIDIポート数: {portCount}");
            
            int gx61PortIndex = -1;
            
            // すべてのポートをスキャンしてGX61を探す
            for (int i = 0; i < portCount; i++)
            {
                var portName = midiIn.GetPortName(i);
                Debug.Log($"ポート {i}: {portName}");
                
                // Impact GX61を探す（名前のバリエーションに対応）
                if (portName.ToLower().Contains("impact") || 
                    portName.ToLower().Contains("gx61") || 
                    portName.ToLower().Contains("gx 61") ||
                    portName.ToLower().Contains("nectar"))
                {
                    Debug.Log($"★ Impact GX61 を検出しました！ポート: {portName}");
                    gx61PortIndex = i;
                    break;
                }
            }
            
            // GX61が見つからない場合、最初のポートを試す
            if (gx61PortIndex == -1 && portCount > 0)
            {
                Debug.LogWarning("Impact GX61が名前で検出できませんでした。");
                Debug.LogWarning("最初のMIDIポートを使用します。");
                gx61PortIndex = 0;
            }
            
            if (gx61PortIndex >= 0)
            {
                ConnectToPort(gx61PortIndex);
            }
            else
            {
                Debug.LogError("使用可能なMIDIポートがありません");
                StartCoroutine(RetryConnection());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RtMidi初期化エラー: {e.Message}");
        }
    }
    
    void ConnectToPort(int portIndex)
    {
        try
        {
            var portName = midiIn.GetPortName(portIndex);
            midiIn.OpenPort(portIndex, "Unity GX61 Input");
            Debug.Log($"ポート {portIndex} ({portName}) を開きました");
            
            // コールバックを設定
            midiIn.MessageReceived = OnMidiMessage;
            
            // 不要なメッセージをフィルタ
            midiIn.IgnoreTypes(sysex: true, time: true, sense: true);
            
            isGX61Connected = true;
            Debug.Log("Impact GX61 の接続に成功しました！");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ポート接続エラー: {e.Message}");
        }
    }
    
    void Update()
    {
        // キューに溜まったMIDIイベントを高速処理
        int processedCount = 0;
        while (midiEventQueue.TryDequeue(out MidiEvent midiEvent) && processedCount < 10)
        {
            ProcessMidiEvent(midiEvent);
            processedCount++;
        }
    }
    
    void LateUpdate()
    {
        // 残りのイベントも処理（フレーム内で2回処理）
        while (midiEventQueue.TryDequeue(out MidiEvent midiEvent))
        {
            ProcessMidiEvent(midiEvent);
        }
    }
    
    void ProcessMidiEvent(MidiEvent midiEvent)
    {
        switch (midiEvent.messageType)
        {
            case 0x90: // Note On
                if (midiEvent.velocity > 0)
                {
                    float normalizedVelocity = midiEvent.velocity / 127f;
                    PlayNoteImmediate(midiEvent.note, normalizedVelocity);
                    
                    // ビジュアルエフェクトを再生
                    if (visualizer != null)
                    {
                        visualizer.PlayNoteEffect(midiEvent.note, normalizedVelocity);
                    }
                    
                    // ノートゲームに入力を通知
                    if (noteGame != null)
                    {
                        noteGame.OnMidiNotePressed(midiEvent.note, normalizedVelocity);
                    }
                }
                else
                {
                    StopNote(midiEvent.note);
                }
                break;
                
            case 0x80: // Note Off
                StopNote(midiEvent.note);
                break;
        }
    }
    
    void OnMidiMessage(double timestamp, System.ReadOnlySpan<byte> message)
    {
        if (message.Length < 2) return;
        
        byte status = message[0];
        byte data1 = message[1];
        byte data2 = message.Length > 2 ? message[2] : (byte)0;
        
        // MIDIチャンネルを無視してメッセージタイプを取得
        byte messageType = (byte)(status & 0xF0);
        byte channel = (byte)((status & 0x0F) + 1);
        
        // MIDIイベントをキューに追加（メインスレッドで処理するため）
        var midiEvent = new MidiEvent
        {
            messageType = messageType,
            note = data1,
            velocity = data2,
            channel = channel
        };
        
        // キューに即座に追加（デバッグログは最小限に）
        midiEventQueue.Enqueue(midiEvent);
    }
    
    void HandleControlChange(byte controller, byte value)
    {
        // よく使われるコントローラー
        switch (controller)
        {
            case 1: // Modulation Wheel
                Debug.Log($"[GX61] モジュレーションホイール: {value}");
                break;
            case 7: // Volume
                Debug.Log($"[GX61] ボリューム: {value}");
                break;
            case 64: // Sustain Pedal
                Debug.Log($"[GX61] サステインペダル: {(value >= 64 ? "ON" : "OFF")}");
                break;
        }
    }
    
    public void PlayNoteImmediate(int midiNote, float velocity)
    {
        // 既存の音を即座に停止
        if (activeSources.ContainsKey(midiNote))
        {
            var oldSource = activeSources[midiNote];
            if (oldSource != null) 
            {
                oldSource.Stop();
                Destroy(oldSource.gameObject);
            }
            activeSources.Remove(midiNote);
        }
        
        // AudioSourceを作成
        GameObject noteObject = new GameObject($"GX61_Note_{midiNote}");
        AudioSource audioSource = noteObject.AddComponent<AudioSource>();
        
        // 事前生成されたクリップがあれば使用、なければ即座に生成
        if (preGeneratedClips.ContainsKey(midiNote))
        {
            audioSource.clip = preGeneratedClips[midiNote];
            audioSource.volume = velocity;
            audioSource.loop = true; // 手動演奏時はループを有効にする
        }
        else
        {
            // リアルタイムで生成（範囲外の音用）
            float frequency = 440f * Mathf.Pow(2f, (midiNote - 69f) / 12f);
            int sampleLength = Mathf.FloorToInt(sampleRate * 0.5f);
            AudioClip clip = AudioClip.Create($"RT_Note_{midiNote}", sampleLength, 1, sampleRate, false);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++)
            {
                float time = (float)i / sampleRate;
                float envelope = time < 0.01f ? time / 0.01f : 1f;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.3f;
            }
            
            clip.SetData(samples, 0);
            audioSource.clip = clip;
            audioSource.volume = velocity;
            
            // 次回用にキャッシュ
            preGeneratedClips[midiNote] = clip;
        }
        
        // 低レイテンシー設定
        audioSource.priority = 0; // 最高優先度
        audioSource.spatialBlend = 0f; // 2Dサウンド
        audioSource.loop = true; // ノートオフまでループ
        audioSource.Play();
        
        activeSources[midiNote] = audioSource;
    }
    
    IEnumerator PlayNoteCoroutine(int midiNote, float velocity)
    {
        Debug.Log($"=== PlayNoteCoroutine開始 - ノート: {midiNote}, ベロシティ: {velocity} ===");
        
        // 既存の音を停止
        if (activeSources.ContainsKey(midiNote))
        {
            var oldSource = activeSources[midiNote];
            if (oldSource != null) Destroy(oldSource.gameObject);
            activeSources.Remove(midiNote);
        }
        
        // テスト音と全く同じ方法で作成
        GameObject noteObject = new GameObject($"GX61_Note_{midiNote}");
        AudioSource audioSource = noteObject.AddComponent<AudioSource>();
        
        // 周波数計算
        float frequency = 440f * Mathf.Pow(2f, (midiNote - 69f) / 12f);
        Debug.Log($"周波数: {frequency}Hz");
        
        // テスト音と同じクリップ生成
        int sampleLength = sampleRate * 1; // 1秒
        AudioClip clip = AudioClip.Create($"Note_{midiNote}", sampleLength, 1, sampleRate, false);
        float[] samples = new float[sampleLength];
        
        for (int i = 0; i < sampleLength; i++)
        {
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * 0.5f;
        }
        
        clip.SetData(samples, 0);
        audioSource.clip = clip;
        audioSource.volume = 1.0f;
        audioSource.Play();
        
        Debug.Log($"再生開始直後の状態: isPlaying={audioSource.isPlaying}");
        
        // 1フレーム待つ
        yield return null;
        
        Debug.Log($"1フレーム後の状態: isPlaying={audioSource.isPlaying}");
        
        activeSources[midiNote] = audioSource;
        
        // 2秒後に自動削除
        yield return new WaitForSeconds(2f);
        if (activeSources.ContainsKey(midiNote) && activeSources[midiNote] == audioSource)
        {
            Destroy(noteObject);
            activeSources.Remove(midiNote);
        }
    }
    
    void PlayNote(int midiNote, float velocity)
    {
        Debug.Log($"PlayNote開始 - ノート: {midiNote}, ベロシティ: {velocity}");
        
        // 既に再生中の音があれば削除
        if (activeSources.ContainsKey(midiNote))
        {
            var oldSource = activeSources[midiNote];
            if (oldSource != null && oldSource.gameObject != null)
            {
                Destroy(oldSource.gameObject);
            }
            activeSources.Remove(midiNote);
        }
        
        // 新しいAudioSourceを作成
        GameObject noteObject = new GameObject($"GX61_Note_{midiNote}");
        noteObject.transform.SetParent(transform);
        AudioSource audioSource = noteObject.AddComponent<AudioSource>();
        
        // 周波数計算（A4 = 440Hz）
        float frequency = 440f * Mathf.Pow(2f, (midiNote - 69f) / 12f);
        Debug.Log($"周波数: {frequency}Hz");
        
        // オーディオクリップを生成（テスト音と同じ方法で）
        int sampleLength = sampleRate * 2; // 2秒
        AudioClip clip = AudioClip.Create($"GX61_Tone_{midiNote}", sampleLength, 1, sampleRate, false);
        
        float[] samples = new float[sampleLength];
        
        // シンプルなサイン波を生成（テスト音と同じ）
        for (int i = 0; i < sampleLength; i++)
        {
            float time = (float)i / sampleRate;
            
            // エンベロープ
            float envelope = 1f;
            if (time < attackTime)
            {
                envelope = time / attackTime;
            }
            
            // 基本のサイン波（音量調整済み）
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.3f;
        }
        
        // データをクリップに設定
        clip.SetData(samples, 0);
        
        // AudioSource設定（テスト音と同じ設定）
        audioSource.clip = clip;
        audioSource.volume = 1.0f;
        audioSource.spatialBlend = 0f; // 2Dサウンド
        audioSource.loop = false; // ループなし
        audioSource.Play();
        
        // 再生状態を確認
        Debug.Log($"AudioSource状態:");
        Debug.Log($"  - isPlaying: {audioSource.isPlaying}");
        Debug.Log($"  - clip: {audioSource.clip != null}");
        Debug.Log($"  - volume: {audioSource.volume}");
        Debug.Log($"  - enabled: {audioSource.enabled}");
        Debug.Log($"  - mute: {audioSource.mute}");
        
        if (audioSource.isPlaying)
        {
            Debug.Log($"✓ 音声再生開始: ノート {midiNote} (周波数: {frequency:F1}Hz)");
        }
        else
        {
            Debug.LogError($"✗ 音声再生に失敗: ノート {midiNote}");
        }
        
        activeSources[midiNote] = audioSource;
    }
    
    public void StopNote(int midiNote)
    {
        if (activeSources.ContainsKey(midiNote))
        {
            AudioSource source = activeSources[midiNote];
            if (source != null)
            {
                StartCoroutine(FadeOutAndDestroy(source, midiNote));
            }
            activeSources.Remove(midiNote);
        }
    }
    
    IEnumerator FadeOutAndDestroy(AudioSource source, int midiNote)
    {
        float startVolume = source.volume;
        float fadeTime = 0f;
        
        while (fadeTime < releaseTime)
        {
            fadeTime += Time.deltaTime;
            float t = fadeTime / releaseTime;
            source.volume = startVolume * (1f - t);
            yield return null;
        }
        
        if (source != null && source.gameObject != null)
        {
            Destroy(source.gameObject);
        }
    }
    
    string GetNoteName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;
        return $"{noteNames[noteIndex]}{octave}";
    }
    
    IEnumerator RetryConnection()
    {
        yield return new WaitForSeconds(5f);
        Debug.Log("MIDI接続を再試行します...");
        InitializeRtMidi();
    }
    
    void OnDestroy()
    {
        // クリーンアップ
        if (midiIn != null && !midiIn.IsInvalid)
        {
            try
            {
                midiIn.ClosePort();
                midiIn.Dispose();
                Debug.Log("MIDIポートを閉じました");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"MIDIポートのクローズエラー: {e.Message}");
            }
        }
        
        // すべてのアクティブなオーディオソースを停止
        foreach (var source in activeSources.Values)
        {
            if (source != null && source.gameObject != null)
            {
                Destroy(source.gameObject);
            }
        }
        activeSources.Clear();
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && midiIn != null && !midiIn.IsInvalid)
        {
            midiIn.ClosePort();
        }
        else if (!pauseStatus && isGX61Connected)
        {
            InitializeRtMidi();
        }
    }
}