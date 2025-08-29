using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.IO;

public class MidiNoteGame : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private string midiFileName = "megalovania"; // .midファイル名（拡張子なし）
    [SerializeField] private float noteSpeed = 5f; // ノーツの落下速度
    [SerializeField] private float judgmentLineY = -3f; // 判定ラインのY座標
    [SerializeField] private float noteLifetime = 8f; // ノーツの生存時間
    [SerializeField] private bool autoPlay = false; // 自動演奏モード
    
    [Header("BGM Settings")]
    [SerializeField] private bool enableBGM = true; // BGMを有効にする
    [Range(0f, 1f)]
    [SerializeField] private float bgmVolume = 0.3f; // BGM音量
    [SerializeField] private bool testBGMOnStart = false; // 起動時にBGMテスト
    
    [Header("Camera Settings")]
    [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 2f, -5f); // カメラ位置
    [SerializeField] private Vector3 cameraRotation = new Vector3(15f, 0f, 0f); // カメラ回転（斜め下を見る）
    
    [Header("Visual Settings")]
    [SerializeField] private GameObject notePrefab; // ノーツのプレハブ
    [SerializeField] private Material noteMaterial; // ノーツのマテリアル
    [SerializeField] private float noteScale = 0.5f; // ノーツのサイズ
    
    [Header("Judgment Settings")]
    [SerializeField] private float perfectRange = 0.1f; // Perfect判定の範囲
    [SerializeField] private float goodRange = 0.3f; // Good判定の範囲
    [SerializeField] private float badRange = 0.6f; // Bad判定の範囲
    
    // ゲーム状態
    private bool isPlaying = false;
    private float gameStartTime = 0f;
    private List<MidiNoteInfo> midiNotes = new List<MidiNoteInfo>();
    private List<NoteObject> activeNotes = new List<NoteObject>();
    private Queue<MidiNoteInfo> upcomingNotes = new Queue<MidiNoteInfo>();
    
    // 色管理用（バランス良く色を配分）
    private int colorRotationIndex = 0;
    
    // 参照
    private KeyboardVisualizer visualizer;
    private SimpleMidiTest midiController;
    private Camera mainCamera;
    private AudioSource bgmAudioSource;
    
    // ノート情報クラス
    [System.Serializable]
    public class MidiNoteInfo
    {
        public int noteNumber;
        public float startTime;
        public float duration;
        public float velocity;
        public Vector3 spawnPosition;
        public Vector3 targetPosition;
    }
    
    // ノートオブジェクトクラス
    public class NoteObject
    {
        public GameObject gameObject;
        public MidiNoteInfo noteInfo;
        public float spawnTime;
        public bool isHit = false;
        public Renderer renderer;
    }
    
    void Start()
    {
        Debug.Log("=== MidiNoteGame 開始 ===");
        
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("メインカメラが見つかりません！");
            return;
        }
        
        // カメラを斜めに設定してノーツを見やすくする
        SetupCamera();
        
        visualizer = FindObjectOfType<KeyboardVisualizer>();
        midiController = FindObjectOfType<SimpleMidiTest>();
        
        Debug.Log($"ビジュアライザー: {(visualizer != null ? "見つかりました" : "見つかりません")}");
        Debug.Log($"MIDIコントローラー: {(midiController != null ? "見つかりました" : "見つかりません")}");
        
        // デフォルトのノーツプレハブを作成
        if (notePrefab == null)
        {
            CreateDefaultNotePrefab();
            Debug.Log("デフォルトノーツプレハブを作成しました");
        }
        
        // MIDIファイルを読み込み
        LoadMidiFile();
        
        if (midiController != null)
        {
            // MIDIコントローラーからの入力を監視
            StartCoroutine(MonitorMidiInput());
        }
        
        // BGM用AudioSourceを作成
        SetupBGMAudioSource();
        
        // BGMを読み込み（少し待ってから）
        StartCoroutine(LoadBGMWithDelay());
        
        // UIの設定
        StartCoroutine(WaitAndStartGame());
    }
    
    void SetupCamera()
    {
        if (mainCamera != null)
        {
            mainCamera.transform.position = cameraPosition;
            mainCamera.transform.rotation = Quaternion.Euler(cameraRotation);
            Debug.Log($"カメラ設定完了 - 位置: {cameraPosition}, 回転: {cameraRotation}");
        }
    }
    
    void SetupBGMAudioSource()
    {
        GameObject bgmObject = new GameObject("BGM_AudioSource");
        bgmObject.transform.SetParent(transform);
        bgmAudioSource = bgmObject.AddComponent<AudioSource>();
        
        // BGM用設定
        bgmAudioSource.loop = true;
        bgmAudioSource.volume = bgmVolume; // Inspector設定を使用
        bgmAudioSource.spatialBlend = 0f; // 2Dサウンド
        bgmAudioSource.priority = 64; // 中程度の優先度
        
        Debug.Log("BGM AudioSource作成完了");
    }
    
    IEnumerator LoadBGMWithDelay()
    {
        yield return new WaitForSeconds(0.5f); // 少し待つ
        LoadBGMAudio();
    }
    
    void LoadBGMAudio()
    {
        Debug.Log($"=== BGM読み込み開始 ===");
        Debug.Log($"enableBGM: {enableBGM}");
        Debug.Log($"bgmAudioSource: {bgmAudioSource != null}");
        
        if (!enableBGM)
        {
            Debug.Log("BGMが無効になっています");
            return;
        }
        
        // 複数の音声形式をサポート
        string[] supportedFormats = { ".wav", ".ogg", ".mp3", ".m4a" };
        string baseFileName = "videoplayback";
        string foundPath = null;
        
        foreach (string format in supportedFormats)
        {
            string testPath = Path.Combine(Application.streamingAssetsPath, "PlaySounds", "Megarovania", baseFileName + format);
            Debug.Log($"音声ファイル確認: {testPath} -> {File.Exists(testPath)}");
            
            if (File.Exists(testPath))
            {
                foundPath = testPath;
                Debug.Log($"✅ 対応音声ファイル発見: {testPath}");
                break;
            }
        }
        
        if (foundPath != null)
        {
            StartCoroutine(LoadBGMCoroutine(foundPath));
        }
        else
        {
            // 代替パスも確認
            foreach (string format in supportedFormats)
            {
                string[] alternatePaths = {
                    Path.Combine(Application.dataPath, "PlaySounds", "Megarovania", baseFileName + format),
                    Path.Combine(Application.dataPath, "StreamingAssets", "PlaySounds", "Megarovania", baseFileName + format)
                };
                
                foreach (string altPath in alternatePaths)
                {
                    Debug.Log($"代替パス確認: {altPath} -> {File.Exists(altPath)}");
                    if (File.Exists(altPath))
                    {
                        StartCoroutine(LoadBGMCoroutine(altPath));
                        return;
                    }
                }
            }
            
            Debug.LogError($"❌ サポートされている音声ファイルが見つかりません。対応形式: {string.Join(", ", supportedFormats)}");
            Debug.LogWarning("音声ファイルをWAV形式に変換してStreamingAssets/PlaySounds/Megarovania/フォルダに配置してください");
        }
    }
    
    IEnumerator LoadBGMCoroutine(string filePath)
    {
        Debug.Log($"=== BGMファイル読み込み開始: {filePath} ===");
        string url = "file://" + filePath;
        Debug.Log($"URL: {url}");
        
        // ファイル拡張子に基づいてAudioTypeを決定
        AudioType[] audioTypes;
        string extension = Path.GetExtension(filePath).ToLower();
        
        switch (extension)
        {
            case ".wav":
                audioTypes = new AudioType[] { AudioType.WAV };
                break;
            case ".ogg":
                audioTypes = new AudioType[] { AudioType.OGGVORBIS };
                break;
            case ".mp3":
                audioTypes = new AudioType[] { AudioType.MPEG };
                break;
            case ".m4a":
                audioTypes = new AudioType[] { AudioType.AUDIOQUEUE, AudioType.MPEG };
                break;
            default:
                audioTypes = new AudioType[] { AudioType.UNKNOWN };
                break;
        }
        
        Debug.Log($"ファイル拡張子: {extension}, 使用AudioType: {string.Join(", ", audioTypes)}");
        
        foreach (AudioType audioType in audioTypes)
        {
            Debug.Log($"AudioType {audioType} で読み込み試行中...");
            
            using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                yield return www.SendWebRequest();
                
                Debug.Log($"リクエスト結果: {www.result}");
                
                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    AudioClip bgmClip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                    Debug.Log($"AudioClip取得: {bgmClip != null}, Length: {(bgmClip != null ? bgmClip.length : 0)}");
                    
                    if (bgmClip != null && bgmClip.length > 0)
                    {
                        bgmAudioSource.clip = bgmClip;
                        Debug.Log($"✅ BGM読み込み成功 ({audioType}): {bgmClip.length}秒, チャンネル: {bgmClip.channels}, 周波数: {bgmClip.frequency}");
                        
                        // テストモードの場合、即座に再生
                        if (testBGMOnStart && bgmAudioSource.clip != null)
                        {
                            Debug.Log("🎵 BGMテスト再生開始（起動時テスト）");
                            bgmAudioSource.Play();
                        }
                        
                        yield break; // 成功したら終了
                    }
                    else
                    {
                        Debug.LogWarning($"AudioClipが無効です ({audioType})");
                    }
                }
                else
                {
                    Debug.LogWarning($"❌ BGM読み込み失敗 ({audioType}): {www.error}");
                }
            }
        }
        
        Debug.LogError("❌ すべてのオーディオフォーマットでBGM読み込みに失敗しました");
    }
    
    void CreateDefaultNotePrefab()
    {
        notePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        notePrefab.name = "DefaultNotePrefab";
        
        // ノートのベースマテリアルを作成
        if (noteMaterial == null)
        {
            noteMaterial = new Material(Shader.Find("Standard"));
            noteMaterial.color = Color.white; // 白をベースにして後で色を変更
            noteMaterial.SetFloat("_Metallic", 0.3f);
            noteMaterial.SetFloat("_Smoothness", 0.7f);
            noteMaterial.EnableKeyword("_EMISSION");
        }
        
        notePrefab.GetComponent<Renderer>().material = noteMaterial;
        notePrefab.transform.localScale = Vector3.one * noteScale;
        notePrefab.SetActive(false);
        
        // プレハブ化
        notePrefab.transform.SetParent(transform);
    }
    
    void LoadMidiFile()
    {
        // StreamingAssetsフォルダのパス確認
        string streamingAssetsPath = Application.streamingAssetsPath;
        string playSoundsPath = Path.Combine(streamingAssetsPath, "PlaySounds");
        string midiPath = Path.Combine(playSoundsPath, "Megarovania", "Undertale_-_Megalovania.mid");
        
        Debug.Log($"StreamingAssets パス: {streamingAssetsPath}");
        Debug.Log($"PlaySounds パス: {playSoundsPath}");  
        Debug.Log($"MIDI ファイルパス: {midiPath}");
        
        if (!Directory.Exists(streamingAssetsPath))
        {
            Debug.LogError($"StreamingAssetsフォルダが存在しません: {streamingAssetsPath}");
            CreateSampleMegalovania();
            return;
        }
        
        if (!File.Exists(midiPath))
        {
            Debug.LogError($"MIDIファイルが見つかりません: {midiPath}");
            
            // テスト用のサンプルノーツを生成（メガロバニアっぽいパターン）
            CreateSampleMegalovania();
            return;
        }
        
        // Melanchall.DryWetMidiが利用できないため、サンプルデータを使用
        Debug.Log("MIDIファイル解析ライブラリが見つからないため、サンプルメガロバニアを使用します");
        CreateSampleMegalovania();
    }
    
    void CreateSampleMegalovania()
    {
        Debug.Log("サンプルメガロバニアパターンを生成");
        
        // メガロバニアの冒頭パターン（より詳細版、2分間のパターン）
        // D D D2 A G# F D A# C のパターンを拡張
        int[] pattern = { 
            62, 62, 74, 69, 68, 65, 62, 58, 60,  // 最初のフレーズ
            62, 62, 74, 69, 68, 65, 62, 58, 60,  // 繰り返し
            65, 65, 77, 72, 71, 68, 65, 61, 63,  // 上の音域で
            62, 62, 74, 69, 68, 65, 62, 58, 60,  // 元に戻る
            // 追加パターン（メロディ展開）
            67, 67, 79, 74, 73, 70, 67, 63, 65,  // G音域
            69, 69, 81, 76, 75, 72, 69, 65, 67,  // A音域
            65, 65, 77, 72, 71, 68, 65, 61, 63,  // F音域
            62, 62, 74, 69, 68, 65, 62, 58, 60   // D音域に戻る
        };
        
        float[] timings = { 
            0f, 0.5f, 1f, 2f, 3f, 4f, 5f, 6f, 7f,
            8f, 8.5f, 9f, 10f, 11f, 12f, 13f, 14f, 15f,
            16f, 16.5f, 17f, 18f, 19f, 20f, 21f, 22f, 23f,
            24f, 24.5f, 25f, 26f, 27f, 28f, 29f, 30f, 31f,
            // より長いメガロバニアパターンを追加
            32f, 32.5f, 33f, 34f, 35f, 36f, 37f, 38f, 39f,
            40f, 40.5f, 41f, 42f, 43f, 44f, 45f, 46f, 47f,
            48f, 48.5f, 49f, 50f, 51f, 52f, 53f, 54f, 55f,
            56f, 56.5f, 57f, 58f, 59f, 60f, 61f, 62f, 63f
        };
        
        for (int i = 0; i < pattern.Length; i++)
        {
            var noteInfo = new MidiNoteInfo
            {
                noteNumber = pattern[i],
                startTime = timings[i],
                duration = 0.4f,
                velocity = 0.8f,
                spawnPosition = CalculateSpawnPosition(pattern[i]),
                targetPosition = CalculateTargetPosition(pattern[i])
            };
            
            midiNotes.Add(noteInfo);
            upcomingNotes.Enqueue(noteInfo);
        }
        
        Debug.Log($"サンプルノーツ生成完了: {midiNotes.Count}個のノーツ、演奏時間: {timings[timings.Length-1] + 0.4f}秒");
    }
    
    Vector3 CalculateSpawnPosition(int noteNumber)
    {
        // ビジュアライザーがない場合でも基本位置を計算
        float xPosition = 0f;
        
        // ノートの番号に応じてX位置を計算（88鍵対応）
        float normalizedNote = (noteNumber - 21f) / 87f; // A0からC8までの範囲
        
        if (mainCamera != null)
        {
            float screenWidth = mainCamera.orthographicSize * 2f * mainCamera.aspect;
            xPosition = -screenWidth * 0.4f + normalizedNote * screenWidth * 0.8f;
        }
        else
        {
            // カメラがない場合の基本計算
            xPosition = -10f + normalizedNote * 20f; // -10から+10の範囲
        }
        
        Vector3 spawnPos = new Vector3(xPosition, 8f, 12f); // より奥の高い位置から開始
        Debug.Log($"ノート {noteNumber} のスポーン位置: {spawnPos}");
        return spawnPos;
    }
    
    Vector3 CalculateTargetPosition(int noteNumber)
    {
        // スポーン位置と同じX計算を使用
        float xPosition = 0f;
        float normalizedNote = (noteNumber - 21f) / 87f;
        
        if (mainCamera != null)
        {
            float screenWidth = mainCamera.orthographicSize * 2f * mainCamera.aspect;
            xPosition = -screenWidth * 0.4f + normalizedNote * screenWidth * 0.8f;
        }
        else
        {
            xPosition = -10f + normalizedNote * 20f;
        }
        
        Vector3 targetPos = new Vector3(xPosition, judgmentLineY, 0f); // 手前の判定ラインへ
        Debug.Log($"ノート {noteNumber} のターゲット位置: {targetPos}");
        return targetPos;
    }
    
    IEnumerator WaitAndStartGame()
    {
        yield return new WaitForSeconds(2f);
        StartGame();
    }
    
    public void StartGame()
    {
        isPlaying = true;
        gameStartTime = Time.time;
        Debug.Log($"ゲーム開始！ 総ノーツ数: {midiNotes.Count}, 待機中ノーツ: {upcomingNotes.Count}");
        
        // BGMを開始
        if (enableBGM && bgmAudioSource != null && bgmAudioSource.clip != null)
        {
            bgmAudioSource.volume = bgmVolume;
            bgmAudioSource.Play();
            Debug.Log($"🎵 BGM再生開始 - Volume: {bgmAudioSource.volume}, Playing: {bgmAudioSource.isPlaying}");
        }
        else
        {
            Debug.LogWarning($"BGM再生失敗 - enableBGM: {enableBGM}, bgmAudioSource: {bgmAudioSource != null}, clip: {(bgmAudioSource?.clip != null)}");
        }
        
        // デバッグ: 最初の数個のノーツ情報を表示
        if (midiNotes.Count > 0)
        {
            for (int i = 0; i < Mathf.Min(5, midiNotes.Count); i++)
            {
                var note = midiNotes[i];
                Debug.Log($"ノーツ {i}: Note={note.noteNumber}, StartTime={note.startTime}, SpawnPos={note.spawnPosition}");
            }
        }
    }
    
    void Update()
    {
        if (!isPlaying) return;
        
        float currentTime = Time.time - gameStartTime;
        
        // 新しいノーツをスポーン
        SpawnUpcomingNotes(currentTime);
        
        // アクティブなノーツを更新
        UpdateActiveNotes(currentTime);
        
        // 自動演奏モード
        if (autoPlay)
        {
            AutoPlayNotes(currentTime);
        }
        
        // BGM状態監視（5秒おきに確認）
        if (Time.time % 5f < 0.1f && bgmAudioSource != null && bgmAudioSource.clip != null)
        {
            if (!bgmAudioSource.isPlaying && enableBGM)
            {
                Debug.LogWarning("⚠️ BGMが停止しています。再開を試行...");
                bgmAudioSource.Play();
            }
        }
    }
    
    void SpawnUpcomingNotes(float currentTime)
    {
        while (upcomingNotes.Count > 0)
        {
            var nextNote = upcomingNotes.Peek();
            float spawnTime = nextNote.startTime - (noteLifetime - 1f); // 1秒前にスポーン
            
            Debug.Log($"現在時刻: {currentTime:F2}, ノーツスポーン時刻: {spawnTime:F2}, ノーツ開始時刻: {nextNote.startTime:F2}");
            
            if (currentTime >= spawnTime)
            {
                Debug.Log($"ノーツスポーン: Note {nextNote.noteNumber} at {currentTime:F2}");
                SpawnNote(upcomingNotes.Dequeue(), currentTime);
            }
            else
            {
                break;
            }
        }
    }
    
    void SpawnNote(MidiNoteInfo noteInfo, float spawnTime)
    {
        GameObject noteObj = Instantiate(notePrefab, noteInfo.spawnPosition, Quaternion.identity);
        noteObj.SetActive(true);
        noteObj.name = $"Note_{noteInfo.noteNumber}_{noteInfo.startTime:F2}";
        
        // ノーツの色を設定
        var renderer = noteObj.GetComponent<Renderer>();
        Color noteColor = GetNoteColor(noteInfo.noteNumber);
        
        // 新しいマテリアルを作成（元のマテリアルをベースに）
        Material newMaterial;
        if (noteMaterial != null)
        {
            newMaterial = new Material(noteMaterial);
        }
        else
        {
            newMaterial = new Material(Shader.Find("Standard"));
        }
        
        // 色を強制的に設定
        newMaterial.color = noteColor;
        newMaterial.SetColor("_BaseColor", noteColor); // URP対応
        newMaterial.SetColor("_Color", noteColor); // Standard Shader対応
        
        // エミッション設定
        newMaterial.EnableKeyword("_EMISSION");
        newMaterial.SetColor("_EmissionColor", noteColor * 0.2f);
        
        // マテリアルを適用
        renderer.material = newMaterial;
        
        Debug.Log($"ノーツマテリアル設定完了: 色={noteColor}, マテリアル名={newMaterial.shader.name}");
        
        var noteObject = new NoteObject
        {
            gameObject = noteObj,
            noteInfo = noteInfo,
            spawnTime = spawnTime,
            isHit = false,
            renderer = renderer
        };
        
        activeNotes.Add(noteObject);
    }
    
    void UpdateActiveNotes(float currentTime)
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            var note = activeNotes[i];
            float noteAge = currentTime - note.spawnTime;
            float progress = noteAge / noteLifetime;
            
            if (progress >= 1f || note.isHit)
            {
                // ノーツを削除
                if (note.gameObject != null)
                {
                    Destroy(note.gameObject);
                }
                activeNotes.RemoveAt(i);
                continue;
            }
            
            // ノーツを移動
            if (note.gameObject != null)
            {
                Vector3 currentPos = Vector3.Lerp(note.noteInfo.spawnPosition, note.noteInfo.targetPosition, progress);
                note.gameObject.transform.position = currentPos;
                
                // 回転エフェクト
                note.gameObject.transform.Rotate(0, 90f * Time.deltaTime, 0);
            }
        }
    }
    
    void AutoPlayNotes(float currentTime)
    {
        foreach (var note in activeNotes)
        {
            if (!note.isHit)
            {
                float timeDifference = Mathf.Abs((currentTime - note.spawnTime) - (note.noteInfo.startTime - note.spawnTime));
                
                if (timeDifference <= perfectRange)
                {
                    HitNote(note, currentTime, "PERFECT");
                    
                    // 音を再生（持続時間付き）
                    if (midiController != null)
                    {
                        StartCoroutine(PlayAutoNote(note.noteInfo.noteNumber, note.noteInfo.velocity, note.noteInfo.duration));
                    }
                    
                    // ビジュアルエフェクト
                    if (visualizer != null)
                    {
                        visualizer.PlayNoteEffect(note.noteInfo.noteNumber, note.noteInfo.velocity);
                    }
                }
            }
        }
    }
    
    IEnumerator PlayAutoNote(int noteNumber, float velocity, float duration)
    {
        // 音を開始（ループ無効でAutoPlay用）
        if (midiController != null)
        {
            midiController.PlayNoteImmediate(noteNumber, velocity, false);
        }
        
        // 持続時間待機
        yield return new WaitForSeconds(duration);
        
        // 音を停止
        if (midiController != null)
        {
            midiController.StopNote(noteNumber);
        }
    }
    
    void HitNote(NoteObject note, float hitTime, string judgment)
    {
        note.isHit = true;
        Debug.Log($"ノート判定: {judgment} - Note {note.noteInfo.noteNumber}");
        
        // エフェクトを追加可能
        if (note.gameObject != null)
        {
            // ヒットエフェクト
            note.renderer.material.SetColor("_EmissionColor", Color.white);
        }
    }
    
    Color GetNoteColor(int noteNumber)
    {
        // カラフルなランダム色の配列
        Color[] vibrantColors = {
            Color.red,
            Color.green, 
            Color.blue,
            Color.yellow,
            Color.magenta,
            Color.cyan,
            new Color(1f, 0.5f, 0f),    // オレンジ
            new Color(0.5f, 0f, 1f),    // 紫
            new Color(1f, 0f, 0.5f),    // ピンク
            new Color(0f, 1f, 0.5f),    // ライムグリーン
            new Color(1f, 1f, 0.5f),    // 薄黄色
            new Color(0.5f, 1f, 1f)     // 薄青
        };
        
        // バランス良く色を配分（順番に使用 + 少しランダム）
        colorRotationIndex = (colorRotationIndex + Random.Range(1, 4)) % vibrantColors.Length;
        Color selectedColor = vibrantColors[colorRotationIndex];
        Debug.Log($"ノート {noteNumber} の色: {selectedColor} (インデックス: {colorRotationIndex})");
        return selectedColor;
    }
    
    IEnumerator MonitorMidiInput()
    {
        while (true)
        {
            // MIDIコントローラーからの入力を監視
            // 実際の入力処理はSimpleMidiTestで処理される
            yield return null;
        }
    }
    
    public void OnMidiNotePressed(int noteNumber, float velocity)
    {
        if (!isPlaying || autoPlay) return;
        
        float currentTime = Time.time - gameStartTime;
        
        // 該当するノーツを探して判定
        NoteObject closestNote = null;
        float closestDistance = float.MaxValue;
        
        foreach (var note in activeNotes)
        {
            if (note.noteInfo.noteNumber == noteNumber && !note.isHit)
            {
                float timeDifference = Mathf.Abs((currentTime - note.spawnTime) - (note.noteInfo.startTime - note.spawnTime));
                
                if (timeDifference < closestDistance && timeDifference <= badRange)
                {
                    closestNote = note;
                    closestDistance = timeDifference;
                }
            }
        }
        
        if (closestNote != null)
        {
            string judgment = "MISS";
            if (closestDistance <= perfectRange)
                judgment = "PERFECT";
            else if (closestDistance <= goodRange)
                judgment = "GOOD";
            else if (closestDistance <= badRange)
                judgment = "BAD";
            
            HitNote(closestNote, currentTime, judgment);
        }
    }
}