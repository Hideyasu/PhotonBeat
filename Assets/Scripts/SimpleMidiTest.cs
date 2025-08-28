using UnityEngine;
using UnityEngine.InputSystem;
using Minis;
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
    private int sampleRate = 44100;
    void Start()
    {
        // MIDI デバイスが接続されたときのコールバック
        InputSystem.onDeviceChange += OnDeviceChange;
        
        // 既に接続されているMIDIデバイスをチェック
        CheckExistingMidiDevices();
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
        // ノートオン（キーを押した時）
        midiDevice.onWillNoteOn += (note, velocity) =>
        {
            string noteName = GetNoteName(note.noteNumber);
            Debug.Log($"[ノートON] キー: {note.noteNumber} ({noteName}) ベロシティ: {velocity} デバイス: {midiDevice.name}");
            PlayNote(note.noteNumber, velocity);
        };
        
        // ノートオフ（キーを離した時）
        midiDevice.onWillNoteOff += (note) =>
        {
            string noteName = GetNoteName(note.noteNumber);
            Debug.Log($"[ノートOFF] キー: {note.noteNumber} ({noteName}) デバイス: {midiDevice.name}");
            StopNote(note.noteNumber);
        };
        
        // コントロールチェンジ（ノブやスライダー）
        midiDevice.onWillControlChange += (control, value) =>
        {
            Debug.Log($"[コントロール] CC{control.controlNumber}: {value} デバイス: {midiDevice.name}");
        };
    }
    
    void PlayNote(int midiNote, float velocity)
    {
        // 既に再生中の音を停止
        if (activeSources.ContainsKey(midiNote))
        {
            StopNote(midiNote);
        }
        
        // 新しいAudioSourceを作成
        GameObject noteObject = new GameObject($"Note_{midiNote}");
        noteObject.transform.SetParent(transform);
        AudioSource audioSource = noteObject.AddComponent<AudioSource>();
        
        // 周波数計算（A4 = 440Hz）
        float frequency = 440f * Mathf.Pow(2f, (midiNote - 69f) / 12f);
        
        // オーディオクリップを生成
        float duration = 5f; // 最大5秒
        AudioClip clip = AudioClip.Create($"Tone_{midiNote}", sampleRate * (int)duration, 1, sampleRate, false);
        
        float[] samples = new float[sampleRate * (int)duration];
        for (int i = 0; i < samples.Length; i++)
        {
            float time = (float)i / sampleRate;
            
            // エンベロープ（アタック）
            float envelope = 1f;
            if (time < attackTime)
            {
                envelope = time / attackTime;
            }
            
            // サイン波生成
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * volume * velocity;
        }
        
        clip.SetData(samples, 0);
        
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.volume = volume * velocity;
        audioSource.Play();
        
        activeSources[midiNote] = audioSource;
    }
    
    void StopNote(int midiNote)
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
    
    System.Collections.IEnumerator FadeOutAndDestroy(AudioSource source, int midiNote)
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
        
        // 全てのアクティブなオーディオソースを停止
        foreach (var source in activeSources.Values)
        {
            if (source != null && source.gameObject != null)
            {
                Destroy(source.gameObject);
            }
        }
        activeSources.Clear();
    }
}