using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

[InitializeOnLoad]
public class MenuPanelBuilder
{
    static MenuPanelBuilder()
    {
        EditorApplication.delayCall += AutoBuildOnce;
    }

    private static void AutoBuildOnce()
    {
        if (!SessionState.GetBool("MenuPanelAutoBuilt_v3", false))
        {
            SessionState.SetBool("MenuPanelAutoBuilt_v3", true);
            BuildMenuPanelUI();
        }
    }

    [MenuItem("Tools/Build Menu Panel UI")]
    public static void BuildMenuPanelUI()
    {
        string scenePath = "Assets/Scenes/UIScene.unity";
        var currentScene = EditorSceneManager.GetActiveScene();

        if (currentScene.path != scenePath)
        {
            if (currentScene.isDirty) EditorSceneManager.SaveScene(currentScene);
            currentScene = EditorSceneManager.OpenScene(scenePath);
        }

        // 1. Locate Canvas & ensure standard 1920x1080 reference
        Canvas canvas = null;
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO != null) canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // 2. Locate or create MenuPanel
        Transform menuPanelTransform = canvas.transform.Find("MenuPanel");
        GameObject menuPanelGO;
        if (menuPanelTransform == null)
        {
            menuPanelGO = new GameObject("MenuPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            menuPanelGO.transform.SetParent(canvas.transform, false);
        }
        else
        {
            menuPanelGO = menuPanelTransform.gameObject;
        }

        menuPanelGO.SetActive(true);

        // Stretch full-screen
        RectTransform panelRT = menuPanelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        panelRT.pivot = new Vector2(0.5f, 0.5f);

        Image panelImg = menuPanelGO.GetComponent<Image>();
        if (panelImg != null && panelImg.sprite == null)
        {
            // Subtle dark tint if no background sprite assigned yet
            panelImg.color = new Color(0.02f, 0.05f, 0.10f, 0.25f);
        }

        // Clean out previous placeholder children
        for (int i = panelRT.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(panelRT.GetChild(i).gameObject);
        }

        // Load Font Asset
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Phantom Studios/AURW/Resources/Fonts/AUDIOWIDE-REGULAR SDF.asset");
        if (font == null)
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset");
        }
        if (font == null)
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        }

        // --- Controller Component ---
        MainMenuController controller = menuPanelGO.GetComponent<MainMenuController>();
        if (controller == null)
        {
            controller = menuPanelGO.AddComponent<MainMenuController>();
        }
        controller.runSceneName = "Run_Scene";

        // ==========================================
        // 1. TOP-LEFT BRAND (TRIATHLON ENDURANCE)
        // ==========================================
        GameObject brandGroup = CreateEmpty("TopLeftBrand", panelRT);
        RectTransform brandRT = brandGroup.GetComponent<RectTransform>();
        SetAnchors(brandRT, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -50), new Vector2(600, 220));

        // Speed Lines + Title
        var titleTMP = CreateTMP("TitleText", brandRT, "≡  TRIATHLON", font, 78, Color.white, TextAlignmentOptions.Left, true, true);
        SetAnchors(titleTMP.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(580, 85));

        // Subtitle: ENDURANCE
        var subTMP = CreateTMP("SubtitleText", brandRT, "E N D U R A N C E", font, 24, new Color(0.49f, 0.83f, 0.99f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(subTMP.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -82), new Vector2(450, 32));

        // Discipline Badges row: SWIM / CYCLE / RUN icons
        GameObject badgesRow = CreateEmpty("DisciplineBadges", brandRT);
        RectTransform badgesRT = badgesRow.GetComponent<RectTransform>();
        SetAnchors(badgesRT, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -125), new Vector2(400, 35));

        var swimBadge = CreateTMP("SwimBadge", badgesRT, "🏊 SWIM", font, 16, new Color(0f, 0.9f, 1f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(swimBadge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(110, 30));

        var cycleBadge = CreateTMP("CycleBadge", badgesRT, "🚴 CYCLE", font, 16, new Color(1f, 0.57f, 0f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(cycleBadge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(120, 30));

        var runBadge = CreateTMP("RunBadge", badgesRT, "🏃 RUN", font, 16, new Color(1f, 0.24f, 0f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(runBadge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(250, 0), new Vector2(110, 30));

        // ==========================================
        // 2. TOP-RIGHT QUICK ACTION ICON BUTTONS
        // ==========================================
        GameObject iconsGroup = CreateEmpty("TopRightControls", panelRT);
        RectTransform iconsRT = iconsGroup.GetComponent<RectTransform>();
        SetAnchors(iconsRT, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, -55), new Vector2(200, 60));

        // Settings Button
        GameObject settingsBtn = CreateIconButton("SettingsButton", iconsRT, "⚙", font, 24, new Vector2(-130, 0));
        controller.settingsButton = settingsBtn.GetComponent<Button>();

        // Audio Button
        GameObject audioBtn = CreateIconButton("AudioButton", iconsRT, "🔊", font, 22, new Vector2(-65, 0));
        controller.audioButton = audioBtn.GetComponent<Button>();

        // Language Button
        GameObject langBtn = CreateIconButton("LanguageButton", iconsRT, "🌐", font, 22, new Vector2(0, 0));

        // ==========================================
        // 3. CENTER GROUP (SLOGAN + MENU BUTTONS)
        // ==========================================
        GameObject centerGroup = CreateEmpty("CenterGroup", panelRT);
        RectTransform centerRT = centerGroup.GetComponent<RectTransform>();
        SetAnchors(centerRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 500));

        // Tagline: THREE DISCIPLINES. ONE FINISH.
        var taglineTMP = CreateTMP("TaglineText", centerRT, "—   THREE DISCIPLINES. ONE FINISH.   —", font, 18, new Color(0.70f, 0.78f, 0.88f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(taglineTMP.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(560, 32));

        // Primary: START RACE >
        GameObject startBtnObj = CreateEmpty("StartButton", centerRT);
        RectTransform startBtnRT = startBtnObj.GetComponent<RectTransform>();
        SetAnchors(startBtnRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(450, 78));

        Image startBg = startBtnObj.AddComponent<Image>();
        startBg.color = new Color(0f, 0.84f, 1f, 1f); // Electric Cyan

        Outline startOutline = startBtnObj.AddComponent<Outline>();
        startOutline.effectColor = new Color(0.22f, 0.74f, 0.97f, 1f);
        startOutline.effectDistance = new Vector2(2, 2);

        Button startBtn = startBtnObj.AddComponent<Button>();
        ColorBlock startCb = startBtn.colors;
        startCb.normalColor = Color.white;
        startCb.highlightedColor = new Color(0.7f, 0.95f, 1f, 1f);
        startCb.pressedColor = new Color(0.01f, 0.55f, 0.8f, 1f);
        startBtn.colors = startCb;

        var startText = CreateTMP("Text", startBtnRT, "START RACE  >", font, 30, new Color(0.01f, 0.08f, 0.16f, 1f), TextAlignmentOptions.Center, true, true);
        SetFullStretch(startText.rectTransform);

        controller.startButton = startBtn;

        // Secondary: HOW TO PLAY
        GameObject howToPlayBtnObj = CreateButton("HowToPlayButton", centerRT, "🎮   HOW TO PLAY", font, 20,
            new Color(0.03f, 0.11f, 0.22f, 0.88f),
            new Color(0.01f, 0.55f, 0.85f, 1f),
            new Color(0.88f, 0.95f, 1f, 1f),
            new Vector2(0, -75), new Vector2(430, 64));
        controller.howToPlayButton = howToPlayBtnObj.GetComponent<Button>();

        // Secondary: LEADERBOARD
        GameObject leaderboardBtnObj = CreateButton("LeaderboardButton", centerRT, "🏆   LEADERBOARD", font, 20,
            new Color(0.12f, 0.11f, 0.05f, 0.88f),
            new Color(0.96f, 0.62f, 0.05f, 1f),
            new Color(1f, 0.84f, 0.31f, 1f),
            new Vector2(0, -155), new Vector2(430, 64));
        controller.leaderboardButton = leaderboardBtnObj.GetComponent<Button>();

        // Discipline Tracker line below buttons
        var trackerTMP = CreateTMP("DisciplineTracker", centerRT, "<color=#00E5FF>SWIM</color>     <color=#64748B>/</color>     <color=#FF9100>CYCLE</color>     <color=#64748B>/</color>     <color=#FF3D00>RUN</color>",
            font, 17, Color.white, TextAlignmentOptions.Center, true, false);
        SetAnchors(trackerTMP.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(500, 32));

        // ==========================================
        // 4. BOTTOM BAR (VERSION & PERSONAL BEST)
        // ==========================================
        GameObject bottomBar = CreateEmpty("BottomBar", panelRT);
        RectTransform bottomRT = bottomBar.GetComponent<RectTransform>();
        SetAnchors(bottomRT, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 90));

        // Version text (Left)
        var versionTMP = CreateTMP("VersionText", bottomRT, "VERSION 1.0.0", font, 15, new Color(0.42f, 0.50f, 0.60f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(versionTMP.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(250, 30));
        controller.versionText = versionTMP;

        // Personal best text (Right)
        var pbTMP = CreateTMP("PersonalBestText", bottomRT, "⏱  PERSONAL BEST   04:12", font, 16, new Color(0.58f, 0.77f, 0.99f, 1f), TextAlignmentOptions.Right, true, false);
        SetAnchors(pbTMP.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(350, 30));
        controller.personalBestText = pbTMP;

        // Hook up OnClick persistent listener for StartButton
        for (int i = startBtn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(startBtn.onClick, i);
        }
        UnityEventTools.AddPersistentListener(startBtn.onClick, controller.OnStartClicked);
        EditorUtility.SetDirty(startBtn);

        // Save & dirty scene
        EditorUtility.SetDirty(menuPanelGO);
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);

        Debug.Log("[MenuPanelBuilder] Menu Panel UI successfully built in UIScene!");
    }

    // ==========================================
    // HELPER METHODS
    // ==========================================
    private static GameObject CreateEmpty(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateButton(string name, Transform parent, string label, TMP_FontAsset font, float fontSize,
        Color bgColor, Color borderColor, Color textColor, Vector2 pos, Vector2 size)
    {
        GameObject btnObj = CreateEmpty(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        SetAnchors(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        Image bg = btnObj.AddComponent<Image>();
        bg.color = bgColor;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2, 2);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
        cb.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        btn.colors = cb;

        var text = CreateTMP("Text", rt, label, font, fontSize, textColor, TextAlignmentOptions.Center, true, false);
        SetFullStretch(text.rectTransform);

        return btnObj;
    }

    private static GameObject CreateIconButton(string name, Transform parent, string icon, TMP_FontAsset font, float fontSize, Vector2 pos)
    {
        GameObject btnObj = CreateEmpty(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        SetAnchors(rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), pos, new Vector2(52, 52));

        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.12f, 0.22f, 0.85f);

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.01f, 0.55f, 0.85f, 0.8f);
        outline.effectDistance = new Vector2(2, 2);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
        cb.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        btn.colors = cb;

        var text = CreateTMP("Icon", rt, icon, font, fontSize, new Color(0.88f, 0.95f, 1f, 1f), TextAlignmentOptions.Center, true, false);
        SetFullStretch(text.rectTransform);

        return btnObj;
    }

    private static TextMeshProUGUI CreateTMP(string name, Transform parent, string text, TMP_FontAsset font, float fontSize, Color color,
        TextAlignmentOptions alignment, bool bold, bool italic)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;

        FontStyles style = FontStyles.Normal;
        if (bold) style |= FontStyles.Bold;
        if (italic) style |= FontStyles.Italic;
        tmp.fontStyle = style;

        return tmp;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void SetFullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}
