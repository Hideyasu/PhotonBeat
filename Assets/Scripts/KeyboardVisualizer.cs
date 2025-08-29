using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class KeyboardVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private GameObject effectPrefab; // エフェクトのプレハブ（後で設定）
    [SerializeField] private float effectDuration = 3f;
    [SerializeField] private float effectHeight = 15f;
    [SerializeField] private float effectSpeed = 8f;
    
    [Header("Light Beam Settings")]
    [SerializeField] [Range(0.5f, 5f)] private float beamWidth = 0.5f; // 光の基本太さ
    [SerializeField] [Range(0.5f, 3f)] private float beamWidthMultiplier = 0.5f; // ベロシティによる太さの倍率
    [SerializeField] [Range(0.1f, 2f)] private float beamHorizontalScale = 0.5f; // 横幅のスケール
    
    [Header("Keyboard Layout")]
    [SerializeField] private int startNote = 21; // A0 (88鍵の最低音)
    [SerializeField] private int endNote = 108; // C8 (88鍵の最高音)
    [SerializeField] private float keyboardWidthOffset = 0.95f; // 画面幅の95%使用
    
    public enum ColorMode
    {
        Gradient,       // ノートの高さに応じたグラデーション
        RandomCyber    // ランダムなサイバーカラー
    }
    
    [Header("Color Settings")]
    [SerializeField] private ColorMode colorMode = ColorMode.RandomCyber;
    [SerializeField] private Gradient noteColorGradient; // ノートの高さに応じた色
    [SerializeField] private float colorIntensity = 2f;
    
    // 明るいサイバーカラーのプリセット
    private Color[] cyberColors = new Color[]
    {
        new Color(0f, 1f, 1f),      // シアン
        new Color(1f, 0f, 1f),      // マゼンタ
        new Color(0f, 0.8f, 1f),    // スカイブルー
        new Color(1f, 0.2f, 0.8f),  // ホットピンク
        new Color(0.5f, 1f, 0f),    // ライムグリーン
        new Color(1f, 1f, 0f),      // イエロー
        new Color(1f, 0.5f, 0f),    // オレンジ
        new Color(0.8f, 0f, 1f),    // パープル
        new Color(0f, 1f, 0.5f),    // ミントグリーン
        new Color(1f, 0.3f, 0.3f),  // コーラルレッド
        new Color(0.3f, 0.8f, 1f),  // アクアブルー
        new Color(1f, 0.8f, 0f),    // ゴールド
        new Color(0.5f, 0f, 1f),    // バイオレット
        new Color(0f, 1f, 0.8f),    // ターコイズ
        new Color(1f, 0f, 0.5f),    // ローズ
    };
    
    private Camera mainCamera;
    private float screenWidth;
    private float keyWidth;
    private int totalKeys;
    
    // エフェクトプール
    private Queue<GameObject> effectPool = new Queue<GameObject>();
    private List<VisualEffect> activeEffects = new List<VisualEffect>();
    
    private class VisualEffect
    {
        public GameObject gameObject;
        public float startTime;
        public float duration;
        public Vector3 startPosition;
        public Vector3 velocity;
        public ParticleSystem particleSystem;
        public Light lightComponent;
    }
    
    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Cameraが見つかりません！");
            return;
        }
        
        CalculateKeyboardDimensions();
        
        // デフォルトのグラデーションを設定（サイバーネオン風）
        if (noteColorGradient == null || noteColorGradient.colorKeys.Length == 0)
        {
            noteColorGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[5];
            colorKeys[0] = new GradientColorKey(new Color(0f, 0.5f, 1f), 0.0f); // ネオンブルー
            colorKeys[1] = new GradientColorKey(new Color(0f, 1f, 1f), 0.25f); // シアン
            colorKeys[2] = new GradientColorKey(new Color(1f, 0f, 1f), 0.5f); // マゼンタ
            colorKeys[3] = new GradientColorKey(new Color(1f, 0.5f, 0f), 0.75f); // オレンジ
            colorKeys[4] = new GradientColorKey(new Color(1f, 0f, 0.5f), 1.0f); // ピンク
            
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);
            
            noteColorGradient.SetKeys(colorKeys, alphaKeys);
        }
        
        // エフェクトプレハブが設定されていない場合、シンプルなものを作成
        if (effectPrefab == null)
        {
            CreateDefaultEffectPrefab();
        }
        
        // エフェクトプールを初期化
        InitializeEffectPool(20);
    }
    
    void CalculateKeyboardDimensions()
    {
        // 画面の幅を計算
        float screenHeight = 2f * mainCamera.orthographicSize;
        float screenAspect = (float)Screen.width / Screen.height;
        screenWidth = screenHeight * screenAspect;
        
        // 88鍵盤の白鍵数を計算（88鍵 = 白鍵52個）
        int whiteKeyCount = 52;
        totalKeys = endNote - startNote + 1;
        
        // 白鍵基準でキー幅を計算
        keyWidth = (screenWidth * keyboardWidthOffset) / whiteKeyCount;
        
        Debug.Log($"画面幅: {screenWidth}, 総キー数: {totalKeys}, 白鍵数: {whiteKeyCount}, キー幅: {keyWidth}");
    }
    
    void CreateDefaultEffectPrefab()
    {
        // シンプルな光るオブジェクトを作成
        effectPrefab = new GameObject("EffectPrefab");
        
        // パーティクルシステムを追加
        ParticleSystem ps = effectPrefab.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 3f;
        main.startSpeed = 0f; // 初速を0にして、velocityOverLifetimeで制御
        main.startSize = new ParticleSystem.MinMaxCurve(1f, 2f); // より太く
        main.maxParticles = 1000;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // ワールド空間でシミュレート
        
        var emission = ps.emission;
        emission.rateOverTime = 100; // より密度を上げる
        
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(beamHorizontalScale, 0.01f, 0.01f); // 横幅を調整可能に
        
        // 真上に直進するように設定
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World; // ワールド空間
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f); // X方向の速度0
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(12f); // Y方向のみ速度設定
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f); // Z方向の速度0
        
        // ネオン風の色とフェードアウト
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(Color.white, 0.0f), // 開始時は白
                new GradientColorKey(Color.white, 0.3f), // しばらく明るく
                new GradientColorKey(Color.white * 0.5f, 0.7f), // 徐々に暗く
                new GradientColorKey(Color.clear, 1.0f) // 最後は透明
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(1.0f, 0.0f), // 開始時は不透明
                new GradientAlphaKey(0.8f, 0.3f),
                new GradientAlphaKey(0.3f, 0.7f), // 上部で薄く
                new GradientAlphaKey(0.0f, 1.0f) // 最後は完全に透明
            }
        );
        colorOverLifetime.color = gradient;
        
        // サイズの変化（上に行くほど広がる）
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(0.5f, 1.2f);
        sizeCurve.AddKey(1f, 1.5f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        
        // パーティクルのレンダラー設定（ネオングロー効果）
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.SetFloat("_Mode", 3); // Transparent
        renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // 加算合成でグロー効果
        renderer.material.EnableKeyword("_ALPHABLEND_ON");
        renderer.renderMode = ParticleSystemRenderMode.Stretch; // 縦に伸びる
        renderer.velocityScale = 0.1f; // ストレッチの長さ
        renderer.lengthScale = 2f; // 縦方向のスケール
        
        // ライトを追加（ネオンの光源）
        Light light = effectPrefab.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = 5f; // より明るく
        light.range = 8f; // より広範囲
        
        effectPrefab.SetActive(false);
    }
    
    void InitializeEffectPool(int poolSize)
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject effect = Instantiate(effectPrefab, transform);
            effect.SetActive(false);
            effectPool.Enqueue(effect);
        }
    }
    
    public void PlayNoteEffect(int midiNote, float velocity)
    {
        // ノートが表示範囲内かチェック
        if (midiNote < startNote || midiNote > endNote)
            return;
        
        // ノート位置を計算（黒鍵も考慮）
        float xPosition = CalculateNoteXPosition(midiNote);
        float yPosition = -mainCamera.orthographicSize - 1f; // 画面下端より少し下から開始
        Vector3 worldPosition = new Vector3(xPosition, yPosition, 0);
        
        // 色を決定（モードによって切り替え）
        Color noteColor;
        if (colorMode == ColorMode.RandomCyber)
        {
            // ランダムなサイバーカラーを選択
            int randomIndex = Random.Range(0, cyberColors.Length);
            noteColor = cyberColors[randomIndex];
            
            // さらに色相を少しランダムに変化させて、より多様性を持たせる
            float hueShift = Random.Range(-0.1f, 0.1f);
            Color.RGBToHSV(noteColor, out float h, out float s, out float v);
            h = Mathf.Repeat(h + hueShift, 1f);
            noteColor = Color.HSVToRGB(h, s, v);
        }
        else
        {
            // グラデーションモード（従来の処理）
            float normalizedNote = (float)(midiNote - startNote) / totalKeys;
            noteColor = noteColorGradient.Evaluate(normalizedNote);
        }
        
        noteColor *= colorIntensity * velocity;
        
        // エフェクトを生成
        SpawnEffect(worldPosition, noteColor, velocity);
    }
    
    float CalculateNoteXPosition(int midiNote)
    {
        // 88鍵盤用の正確な位置計算
        // A0 (MIDI 21) から C8 (MIDI 108) まで
        
        // 各音の白鍵からの相対位置を計算
        int noteFromA0 = midiNote - 21; // A0からの距離
        
        // A0からの白鍵の累積数を計算
        float whiteKeyPosition = 0;
        
        for (int i = 21; i <= midiNote; i++)
        {
            int currentNoteInOctave = i % 12;
            // 白鍵の場合カウントアップ（C,D,E,F,G,A,B）
            if (currentNoteInOctave == 0 || currentNoteInOctave == 2 || currentNoteInOctave == 4 || 
                currentNoteInOctave == 5 || currentNoteInOctave == 7 || currentNoteInOctave == 9 || 
                currentNoteInOctave == 11)
            {
                whiteKeyPosition++;
            }
        }
        
        // 黒鍵の位置調整
        int targetNoteInOctave = midiNote % 12;
        float adjustment = 0;
        
        // 黒鍵の場合、前の白鍵からの相対位置を調整
        switch (targetNoteInOctave)
        {
            case 1:  // C#
            case 6:  // F#
                adjustment = -0.35f;
                break;
            case 3:  // D#
            case 10: // A#
                adjustment = -0.65f;
                break;
            case 8:  // G#
                adjustment = -0.5f;
                break;
        }
        
        whiteKeyPosition += adjustment;
        
        // 最初の白鍵（A0）を左端に配置
        whiteKeyPosition -= 1; // A0を0位置にする
        
        // 画面座標に変換（52白鍵が画面幅に収まるように）
        float normalizedPosition = whiteKeyPosition / 52.0f;
        float xPosition = -screenWidth * keyboardWidthOffset * 0.5f + normalizedPosition * screenWidth * keyboardWidthOffset;
        
        return xPosition;
    }
    
    void SpawnEffect(Vector3 position, Color color, float velocity)
    {
        GameObject effectObject;
        
        if (effectPool.Count > 0)
        {
            effectObject = effectPool.Dequeue();
        }
        else
        {
            effectObject = Instantiate(effectPrefab, transform);
        }
        
        effectObject.transform.position = position;
        effectObject.SetActive(true);
        
        // パーティクルシステムの色を設定
        ParticleSystem ps = effectObject.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            // ネオンカラーをHDR（高輝度）で設定
            Color hdrColor = color * (2f + velocity * 3f); // HDR強度を上げる
            main.startColor = hdrColor;
            
            // Inspectorで設定可能な太さを適用
            float minSize = beamWidth + velocity * beamWidthMultiplier;
            float maxSize = (beamWidth * 1.5f) + velocity * (beamWidthMultiplier * 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startLifetime = 2.5f + velocity * 1f;
            
            var emission = ps.emission;
            emission.rateOverTime = 80 + velocity * 120; // より密度を上げる
            
            // バーストを追加（初期の爆発的なパーティクル）
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, (short)(30 + velocity * 50)), // より多くのバースト
                new ParticleSystem.Burst(0.1f, (short)(20 + velocity * 30)) // 2段階バースト
            });
            
            // 速度を調整（真上のみ）
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(10f + velocity * 5f);
            
            // 横幅のスケールも動的に調整
            var shape = ps.shape;
            shape.scale = new Vector3(beamHorizontalScale, 0.01f, 0.01f);
            
            ps.Clear();
            ps.Play();
        }
        
        // ライトの色を設定（ネオングロー）
        Light light = effectObject.GetComponent<Light>();
        if (light != null)
        {
            light.color = color;
            light.intensity = 4f + velocity * 8f; // より明るく
            light.range = 6f + velocity * 6f; // より広範囲
        }
        
        // アクティブエフェクトとして登録
        VisualEffect vfx = new VisualEffect
        {
            gameObject = effectObject,
            startTime = Time.time,
            duration = effectDuration,
            startPosition = position,
            velocity = Vector3.up * effectSpeed,
            particleSystem = ps,
            lightComponent = light
        };
        
        activeEffects.Add(vfx);
        StartCoroutine(DeactivateEffectAfterTime(vfx));
    }
    
    IEnumerator DeactivateEffectAfterTime(VisualEffect effect)
    {
        yield return new WaitForSeconds(effect.duration);
        
        if (effect.gameObject != null)
        {
            effect.gameObject.SetActive(false);
            effectPool.Enqueue(effect.gameObject);
            activeEffects.Remove(effect);
        }
    }
    
    void Update()
    {
        // アクティブなエフェクトのライト強度を時間とともに減衰
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            if (effect.lightComponent != null)
            {
                float elapsed = Time.time - effect.startTime;
                float normalizedTime = elapsed / effect.duration;
                effect.lightComponent.intensity = Mathf.Lerp(effect.lightComponent.intensity, 0, normalizedTime);
            }
        }
    }
}