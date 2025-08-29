using UnityEngine;
using UnityEngine.UI;

public class BackgroundManager : MonoBehaviour
{
    [Header("Background Settings")]
    [SerializeField] private Canvas backgroundCanvas;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite megarovaniaBackground;
    
    void Start()
    {
        SetupBackground();
        LoadMegarovaniaBackground();
    }
    
    void SetupBackground()
    {
        // Canvasが設定されていない場合は作成
        if (backgroundCanvas == null)
        {
            GameObject canvasObject = new GameObject("BackgroundCanvas");
            backgroundCanvas = canvasObject.AddComponent<Canvas>();
            backgroundCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            backgroundCanvas.worldCamera = Camera.main;
            backgroundCanvas.planeDistance = 100f; // カメラから遠い位置に配置
            backgroundCanvas.sortingOrder = -100; // 背景として最背面に配置
            
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasObject.AddComponent<GraphicRaycaster>();
        }
        
        // Image コンポーネントが設定されていない場合は作成
        if (backgroundImage == null)
        {
            GameObject imageObject = new GameObject("BackgroundImage");
            imageObject.transform.SetParent(backgroundCanvas.transform, false);
            
            backgroundImage = imageObject.AddComponent<Image>();
            
            // 画面全体をカバーするように設定
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
    
    void LoadMegarovaniaBackground()
    {
        // Resourcesフォルダから背景画像を読み込み
        Texture2D backgroundTexture = Resources.Load<Texture2D>("PlaySounds/Megarovania/background");
        
        if (backgroundTexture == null)
        {
            // Resourcesで見つからない場合は直接パスから読み込みを試行
            string imagePath = "Assets/PlaySounds/Megarovania/background.jpg";
            backgroundTexture = LoadTextureFromAssets(imagePath);
        }
        
        if (backgroundTexture != null)
        {
            // TextureからSpriteを作成
            megarovaniaBackground = Sprite.Create(
                backgroundTexture,
                new Rect(0, 0, backgroundTexture.width, backgroundTexture.height),
                new Vector2(0.5f, 0.5f)
            );
            
            // 背景画像を設定
            backgroundImage.sprite = megarovaniaBackground;
            backgroundImage.preserveAspect = true;
            
            // 背景を薄くして、前景のエフェクトが見えやすくする
            Color backgroundColor = backgroundImage.color;
            backgroundColor.a = 0.6f; // 透明度を60%に設定
            backgroundImage.color = backgroundColor;
            
            Debug.Log("Megarovania background loaded successfully!");
        }
        else
        {
            Debug.LogError("Failed to load Megarovania background image!");
        }
    }
    
    Texture2D LoadTextureFromAssets(string path)
    {
        // Unity エディタでのみ動作する方法
        #if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        #else
        return null;
        #endif
    }
    
    public void SetBackground(Sprite newBackground)
    {
        if (backgroundImage != null && newBackground != null)
        {
            backgroundImage.sprite = newBackground;
        }
    }
}