using UnityEngine;
using UnityEngine.InputSystem;
using Minis;
using System.Collections;
using System.Collections.Generic;

public class SimpleMidiTest : MonoBehaviour
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
    
    // ビジュアライザー
    private KeyboardVisualizer visualizer;
    
    // ノートゲーム
    private MidiNoteGame noteGame;
    
    void Awake()
    {
        // 最速で初期化
        Application.targetFrameRate = 120; // フレームレート上限を上げる
    }
    
    void Start()
    {
        Debug.Log("=== SimpleMidiTest (LX88) 起動 ===");
        
        // AudioListenerの確認
        var listener = FindObjectOfType<AudioListener>();
        if (listener == null)
        {
            gameObject.AddComponent<AudioListener>();
            Debug.Log("AudioListenerを自動追加しました。");
        }
        
        // オーディオ設定を低遅延に最適化
        var audioConfig = AudioSettings.GetConfiguration();
        audioConfig.dspBufferSize = 256; // バッファサイズを小さく
        audioConfig.sampleRate = 48000; // 低遅延サンプルレート
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
            GameObject visualizerObject = new GameObject("KeyboardVisualizer_LX88");
            visualizer = visualizerObject.AddComponent<KeyboardVisualizer>();
            Debug.Log("KeyboardVisualizerを作成しました");
        }
        
        // ノートゲームの参照を取得
        noteGame = FindObjectOfType<MidiNoteGame>();
        
        // 全音域のAudioClipを事前生成
        StartCoroutine(PreGenerateAudioClips());
        
        // MIDI デバイスが接続されたときのコールバック
        InputSystem.onDeviceChange += OnDeviceChange;
        
        // 既に接続されているMIDIデバイスをチェック
        CheckExistingMidiDevices();
    }
    
    IEnumerator PreGenerateAudioClips()
    {
        Debug.Log("AudioClip事前生成開始...");
        
        // 全MIDI音域（0-127）を事前生成
        for (int note = 0; note < 128; note++)
        {
            float frequency = 440f * Mathf.Pow(2f, (note - 69f) / 12f);
            
            // 自然な減衰付きクリップ（1.5秒）
            int sampleLength = Mathf.FloorToInt(sampleRate * 1.5f);
            AudioClip clip = AudioClip.Create($"PreGen_Note_{note}", sampleLength, 1, sampleRate, false);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++)
            {
                float time = (float)i / sampleRate;
                // ADSR エンベロープ
                float envelope = 1f;
                if (time < 0.01f) // アタック（10ms）
                {
                    envelope = time / 0.01f;
                }
                else if (time > 0.8f) // リリース（0.8秒後から減衰）
                {
                    envelope = Mathf.Exp(-4f * (time - 0.8f)); // 指数的減衰
                }
                
                // よりリッチな音色（基音＋倍音）
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.25f;
                samples[i] += Mathf.Sin(4f * Mathf.PI * frequency * time) * envelope * 0.1f;
                samples[i] += Mathf.Sin(8f * Mathf.PI * frequency * time) * envelope * 0.05f;
            }
            
            clip.SetData(samples, 0);
            preGeneratedClips[note] = clip;
            
            // 8音ごとに1フレーム待つ
            if (note % 8 == 0) yield return null;
        }
        
        Debug.Log($"AudioClip事前生成完了: 全{preGeneratedClips.Count}音");
    }
    
    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is MidiDevice midiDevice)
        {
            if (change == InputDeviceChange.Added)
            {
                Debug.Log($"MIDI デバイスが接続されました: {midiDevice.name} (チャンネル: {midiDevice.channel})");
                SubscribeToMidiEvents(midiDevice);
            }
            else if (change == InputDeviceChange.Removed)
            {
                Debug.Log($"MIDI デバイスが切断されました: {midiDevice.name}");
                UnsubscribeFromMidiEvents(midiDevice);
            }
        }
    }
    
    void CheckExistingMidiDevices()
    {
        var allDevices = InputSystem.devices;
        int midiDeviceCount = 0;
        
        foreach (var device in allDevices)
        {
            if (device is MidiDevice midiDevice)
            {
                midiDeviceCount++;
                Debug.Log($"MIDI デバイス: {midiDevice.name} (チャンネル: {midiDevice.channel})");
                SubscribeToMidiEvents(midiDevice);
            }
        }
        
        Debug.Log($"接続されているMIDIデバイス数: {midiDeviceCount}");
    }
    
    void SubscribeToMidiEvents(MidiDevice midiDevice)
    {
        // デバイスが有効かチェック
        if (midiDevice == null || !midiDevice.enabled) return;
        
        // ノートオン（キーを押した時）
        midiDevice.onWillNoteOn += OnNoteOn;
        
        // ノートオフ（キーを離した時）
        midiDevice.onWillNoteOff += OnNoteOff;
        
        // コントロールチェンジ（ノブやスライダー）
        midiDevice.onWillControlChange += OnControlChange;
    }
    
    void UnsubscribeFromMidiEvents(MidiDevice midiDevice)
    {
        if (midiDevice == null) return;
        
        midiDevice.onWillNoteOn -= OnNoteOn;
        midiDevice.onWillNoteOff -= OnNoteOff;
        midiDevice.onWillControlChange -= OnControlChange;
    }
    
    void OnNoteOn(MidiNoteControl note, float velocity)
    {
        if (this == null) return; // オブジェクトが破棄されていたら何もしない
        
        PlayNoteImmediate(note.noteNumber, velocity);
        
        // ビジュアルエフェクトを再生
        if (visualizer != null)
        {
            visualizer.PlayNoteEffect(note.noteNumber, velocity);
        }
        
        // ノートゲームに入力を通知
        if (noteGame != null)
        {
            noteGame.OnMidiNotePressed(note.noteNumber, velocity);
        }
    }
    
    void OnNoteOff(MidiNoteControl note)
    {
        if (this == null) return; // オブジェクトが破棄されていたら何もしない
        
        StopNote(note.noteNumber);
    }
    
    void OnControlChange(MidiValueControl control, float value)
    {
        // 必要に応じて処理
    }
    
    public void PlayNoteImmediate(int midiNote, float velocity, bool enableLoop = true)
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
        GameObject noteObject = new GameObject($"LX88_Note_{midiNote}");
        noteObject.transform.SetParent(transform);
        AudioSource audioSource = noteObject.AddComponent<AudioSource>();
        
        // 事前生成されたクリップがあれば使用、なければ即座に生成
        if (preGeneratedClips.ContainsKey(midiNote))
        {
            audioSource.clip = preGeneratedClips[midiNote];
            audioSource.volume = velocity * volume; // 全体音量も適用
            audioSource.loop = enableLoop; // 引数でループを制御
        }
        else
        {
            // リアルタイムで生成（範囲外の音用、減衰付き）
            float frequency = 440f * Mathf.Pow(2f, (midiNote - 69f) / 12f);
            int sampleLength = Mathf.FloorToInt(sampleRate * 1.5f); // 1.5秒の音
            AudioClip clip = AudioClip.Create($"RT_Note_{midiNote}", sampleLength, 1, sampleRate, false);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++)
            {
                float time = (float)i / sampleRate;
                // ADSR エンベロープ（アタック・ディケイ・リリース）
                float envelope = 1f;
                if (time < 0.01f) // アタック
                {
                    envelope = time / 0.01f;
                }
                else if (time > 1.0f) // リリース（1秒後から減衰）
                {
                    envelope = Mathf.Exp(-3f * (time - 1.0f)); // 指数的減衰
                }
                
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.3f;
            }
            
            clip.SetData(samples, 0);
            audioSource.clip = clip;
            audioSource.volume = velocity * volume;
            audioSource.loop = false; // リアルタイム生成は減衰付きなのでループ無効
            
            // 次回用にキャッシュ
            preGeneratedClips[midiNote] = clip;
        }
        
        // 低レイテンシー設定
        audioSource.priority = 0; // 最高優先度
        audioSource.spatialBlend = 0f; // 2Dサウンド
        audioSource.Play();
        
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
    
    void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        
        // すべてのMIDIデバイスからイベント登録解除
        var allDevices = InputSystem.devices;
        foreach (var device in allDevices)
        {
            if (device is MidiDevice midiDevice)
            {
                UnsubscribeFromMidiEvents(midiDevice);
            }
        }
        
        // 全てのアクティブなオーディオソースを停止
        foreach (var source in activeSources.Values)
        {
            if (source != null && source.gameObject != null)
            {
                Destroy(source.gameObject);
            }
        }
        activeSources.Clear();
        
        // 事前生成したクリップをクリア
        preGeneratedClips.Clear();
    }
}