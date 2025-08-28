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
    
    [Header("Keyboard Layout")]
    [SerializeField] private int startNote = 36; // C2
    [SerializeField] private int endNote = 96; // C7
    [SerializeField] private float keyboardWidthOffset = 0.9f; // 画面幅の90%使用
    
    [Header("Color Settings")]
    [SerializeField] private Gradient noteColorGradient; // ノートの高さに応じた色
    [SerializeField] private float colorIntensity = 2f;
    
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
        
        // デフォルトのグラデーションを設定
        if (noteColorGradient == null || noteColorGradient.colorKeys.Length == 0)
        {
            noteColorGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[5];
            colorKeys[0] = new GradientColorKey(Color.blue, 0.0f);
            colorKeys[1] = new GradientColorKey(Color.cyan, 0.25f);
            colorKeys[2] = new GradientColorKey(Color.green, 0.5f);
            colorKeys[3] = new GradientColorKey(Color.yellow, 0.75f);
            colorKeys[4] = new GradientColorKey(Color.red, 1.0f);
            
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
        
        // GX61は61鍵だが、表示範囲を設定
        totalKeys = endNote - startNote + 1;
        keyWidth = (screenWidth * keyboardWidthOffset) / totalKeys;
        
        Debug.Log($"画面幅: {screenWidth}, キー数: {totalKeys}, キー幅: {keyWidth}");
    }
    
    void CreateDefaultEffectPrefab()
    {
        // シンプルな光るオブジェクトを作成
        effectPrefab = new GameObject("EffectPrefab");
        
        // パーティクルシステムを追加
        ParticleSystem ps = effectPrefab.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 2.5f;
        main.startSpeed = 10f;
        main.startSize = 0.8f;
        main.maxParticles = 500;
        
        var emission = ps.emission;
        emission.rateOverTime = 50;
        
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.3f, 0.1f, 0.1f);
        
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(8f);
        
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(Color.white, 0.7f),
                new GradientColorKey(Color.clear, 1.0f)
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = gradient;
        
        // パーティクルのレンダラー設定
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.SetFloat("_Mode", 3); // Transparent
        renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        renderer.material.EnableKeyword("_ALPHABLEND_ON");
        
        // ライトを追加
        Light light = effectPrefab.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = 3f;
        light.range = 5f;
        
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
        
        // 色を計算
        float normalizedNote = (float)(midiNote - startNote) / totalKeys;
        Color noteColor = noteColorGradient.Evaluate(normalizedNote);
        noteColor *= colorIntensity * velocity;
        
        // エフェクトを生成
        SpawnEffect(worldPosition, noteColor, velocity);
    }
    
    float CalculateNoteXPosition(int midiNote)
    {
        // 白鍵と黒鍵を考慮した位置計算
        int noteInOctave = midiNote % 12;
        int octave = midiNote / 12;
        
        // 1オクターブ内での位置（白鍵基準）
        float[] keyPositions = {
            0.0f,   // C
            0.5f,   // C#
            1.0f,   // D
            1.5f,   // D#
            2.0f,   // E
            3.0f,   // F
            3.5f,   // F#
            4.0f,   // G
            4.5f,   // G#
            5.0f,   // A
            5.5f,   // A#
            6.0f    // B
        };
        
        // 基準となる位置を計算
        int baseOctave = startNote / 12;
        float relativeOctave = octave - baseOctave;
        float octaveOffset = relativeOctave * 7.0f; // 1オクターブは白鍵7個分
        
        float positionInOctave = keyPositions[noteInOctave];
        float totalPosition = octaveOffset + positionInOctave;
        
        // 画面座標に変換
        float normalizedPosition = totalPosition / (totalKeys * 0.583f); // 61鍵の調整
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
            main.startColor = color;
            main.startSize = 0.5f + velocity * 1f;
            main.startSpeed = effectSpeed * (0.8f + velocity * 0.6f);
            main.startLifetime = 2f + velocity * 1f;
            
            var emission = ps.emission;
            emission.rateOverTime = 30 + velocity * 50;
            
            // バーストを追加（初期の爆発的なパーティクル）
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, (short)(10 + velocity * 20))
            });
            
            ps.Clear();
            ps.Play();
        }
        
        // ライトの色を設定
        Light light = effectObject.GetComponent<Light>();
        if (light != null)
        {
            light.color = color;
            light.intensity = 2f + velocity * 4f;
            light.range = 4f + velocity * 3f;
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