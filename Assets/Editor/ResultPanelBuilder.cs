using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

[InitializeOnLoad]
public class ResultPanelBuilder
{
    static ResultPanelBuilder()
    {
        EditorApplication.delayCall += AutoBuildOnce;
    }

    private static void AutoBuildOnce()
    {
        if (!SessionState.GetBool("ResultPanelAutoBuilt_v3", false))
        {
            SessionState.SetBool("ResultPanelAutoBuilt_v3", true);
            BuildResultPanelUI();
        }
    }

    [MenuItem("Tools/Build Result Panel UI")]
    public static void BuildResultPanelUI()
    {
        string scenePath = "Assets/Scenes/UIScene.unity";
        var currentScene = EditorSceneManager.GetActiveScene();

        if (currentScene.path != scenePath)
        {
            if (currentScene.isDirty) EditorSceneManager.SaveScene(currentScene);
            currentScene = EditorSceneManager.OpenScene(scenePath);
        }

        // 1. Locate or create Canvas
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

        // 2. Locate or create ResultPanel under Canvas
        Transform resultPanelTransform = canvas.transform.Find("ResultPanel");
        GameObject resultPanelGO;
        if (resultPanelTransform == null)
        {
            resultPanelGO = new GameObject("ResultPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            resultPanelGO.transform.SetParent(canvas.transform, false);
        }
        else
        {
            resultPanelGO = resultPanelTransform.gameObject;
        }

        resultPanelGO.SetActive(true);

        // Stretch full-screen
        RectTransform panelRT = resultPanelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        panelRT.pivot = new Vector2(0.5f, 0.5f);

        Image panelImg = resultPanelGO.GetComponent<Image>();
        if (panelImg != null)
        {
            panelImg.color = new Color(0.02f, 0.05f, 0.10f, 0.88f); // Translucent sci-fi dark slate
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
        ResultUIController controller = resultPanelGO.GetComponent<ResultUIController>();
        if (controller == null)
        {
            controller = resultPanelGO.AddComponent<ResultUIController>();
        }

        // ==========================================
        // 1. TOP HEADER SECTION
        // ==========================================
        GameObject headerGroup = CreateEmpty("HeaderGroup", panelRT);
        RectTransform headerRT = headerGroup.GetComponent<RectTransform>();
        SetAnchors(headerRT, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(0, 180));

        // Subheader: /// RACE COMPLETE
        var raceComplete = CreateTMP("RaceCompleteText", headerRT, "///  RACE COMPLETE", font, 24, new Color(0f, 0.9f, 1f, 1f), TextAlignmentOptions.Left, true, true);
        SetAnchors(raceComplete.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -50), new Vector2(400, 40));
        controller.raceCompleteText = raceComplete;

        // Title: VICTORY
        var victory = CreateTMP("VictoryText", headerRT, "VICTORY", font, 78, new Color(1f, 0.84f, 0.31f, 1f), TextAlignmentOptions.Center, true, true);
        SetAnchors(victory.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -55), new Vector2(600, 95));
        controller.victoryText = victory;

        // Badge: NEW BEST!
        GameObject newBestObj = CreateEmpty("NewBestBadge", headerRT);
        RectTransform newBestRT = newBestObj.GetComponent<RectTransform>();
        SetAnchors(newBestRT, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, -55), new Vector2(180, 42));
        var badgeBg = newBestObj.AddComponent<Image>();
        badgeBg.color = new Color(0.20f, 0.15f, 0.05f, 0.85f);
        var badgeOutline = newBestObj.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(1f, 0.75f, 0.1f, 0.9f);
        badgeOutline.effectDistance = new Vector2(2, 2);

        var newBestText = CreateTMP("Text", newBestRT, "NEW BEST!", font, 18, new Color(1f, 0.84f, 0.31f, 1f), TextAlignmentOptions.Center, true, false);
        SetFullStretch(newBestText.rectTransform);
        controller.newBestText = newBestText;

        // ==========================================
        // 2. CENTER TIME DISPLAY BOX
        // ==========================================
        GameObject timeBox = CreateEmpty("CenterTimeBox", panelRT);
        RectTransform timeBoxRT = timeBox.GetComponent<RectTransform>();
        SetAnchors(timeBoxRT, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(720, 88));
        var timeBoxBg = timeBox.AddComponent<Image>();
        timeBoxBg.color = new Color(0.03f, 0.09f, 0.17f, 0.90f);
        var timeBoxOutline = timeBox.AddComponent<Outline>();
        timeBoxOutline.effectColor = new Color(0f, 0.7f, 1f, 0.45f);
        timeBoxOutline.effectDistance = new Vector2(2, 2);

        // Your Time (Left)
        GameObject yourTimeCol = CreateEmpty("YourTimeCol", timeBoxRT);
        RectTransform yourTimeRT = yourTimeCol.GetComponent<RectTransform>();
        SetAnchors(yourTimeRT, new Vector2(0.25f, 0.5f), new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 80));

        var yourTimeLabel = CreateTMP("Label", yourTimeRT, "YOUR TIME", font, 14, new Color(0.5f, 0.85f, 1f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(yourTimeLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(300, 24));

        var yourTimeVal = CreateTMP("Value", yourTimeRT, "04:32.50", font, 42, new Color(0f, 0.96f, 1f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(yourTimeVal.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(300, 48));
        controller.yourTimeValue = yourTimeVal;

        // Divider
        GameObject centerDivider = CreateImage("Divider", timeBoxRT, new Color(0.15f, 0.35f, 0.55f, 0.5f));
        RectTransform divRT = centerDivider.GetComponent<RectTransform>();
        SetAnchors(divRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 54));

        // AI Time (Right)
        GameObject aiTimeCol = CreateEmpty("AITimeCol", timeBoxRT);
        RectTransform aiTimeRT = aiTimeCol.GetComponent<RectTransform>();
        SetAnchors(aiTimeRT, new Vector2(0.75f, 0.5f), new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 80));

        var aiTimeLabel = CreateTMP("Label", aiTimeRT, "AI TIME", font, 14, new Color(0.6f, 0.7f, 0.75f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(aiTimeLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(300, 24));

        var aiTimeVal = CreateTMP("Value", aiTimeRT, "04:41.20", font, 38, new Color(0.92f, 0.94f, 0.96f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(aiTimeVal.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(300, 48));
        controller.aiTimeValue = aiTimeVal;

        // ==========================================
        // 3. TWO CARDS: STAGE SPLITS & STAMINA REPORT
        // ==========================================
        float cardY = -35f;
        float cardW = 660f;
        float cardH = 370f;

        // --- LEFT CARD: STAGE SPLITS ---
        GameObject leftCard = CreateCard("LeftCard_StageSplits", panelRT, new Vector2(-350, cardY), new Vector2(cardW, cardH));
        RectTransform leftCardRT = leftCard.GetComponent<RectTransform>();

        // Header Title
        var splitsTitle = CreateTMP("Title", leftCardRT, ">  STAGE SPLITS", font, 20, new Color(0.1f, 0.9f, 1f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(splitsTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -25), new Vector2(300, 30));

        GameObject splitsLine = CreateImage("Separator", leftCardRT, new Color(0.12f, 0.28f, 0.48f, 0.7f));
        SetAnchors(splitsLine.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -58), new Vector2(580, 2));

        // Swim Row
        controller.swimTimeText = CreateSplitRow(leftCardRT, "SwimRow", "SWIM", "00:48.32", font, new Color(0f, 0.9f, 1f, 1f), -105f);

        // Cycle Row
        controller.cycleTimeText = CreateSplitRow(leftCardRT, "CycleRow", "CYCLE", "01:52.11", font, new Color(1f, 0.6f, 0f, 1f), -165f);

        // Run Row
        controller.runTimeText = CreateSplitRow(leftCardRT, "RunRow", "RUN", "01:52.07", font, new Color(1f, 0.15f, 0.25f, 1f), -225f);

        // Footer separator
        GameObject splitsFooterLine = CreateImage("FooterSeparator", leftCardRT, new Color(0.12f, 0.28f, 0.48f, 0.4f));
        SetAnchors(splitsFooterLine.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -280), new Vector2(580, 1));

        // Penalties Row
        var penaltiesLabel = CreateTMP("PenaltiesLabel", leftCardRT, "⚠  PENALTIES", font, 16, new Color(0.55f, 0.65f, 0.7f, 1f), TextAlignmentOptions.Left, false, false);
        SetAnchors(penaltiesLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(55, -315), new Vector2(200, 30));

        var penaltiesVal = CreateTMP("PenaltiesValue", leftCardRT, "+0.00s", font, 18, new Color(0.7f, 0.75f, 0.8f, 1f), TextAlignmentOptions.Right, true, false);
        SetAnchors(penaltiesVal.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-55, -315), new Vector2(200, 30));
        controller.penaltiesText = penaltiesVal;

        // --- RIGHT CARD: STAMINA REPORT ---
        GameObject rightCard = CreateCard("RightCard_StaminaReport", panelRT, new Vector2(350, cardY), new Vector2(cardW, cardH));
        RectTransform rightCardRT = rightCard.GetComponent<RectTransform>();

        // Header Title
        var staminaTitle = CreateTMP("Title", rightCardRT, ">  STAMINA REPORT", font, 20, new Color(0.1f, 0.9f, 1f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(staminaTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -25), new Vector2(300, 30));

        GameObject staminaLine = CreateImage("Separator", rightCardRT, new Color(0.12f, 0.28f, 0.48f, 0.7f));
        SetAnchors(staminaLine.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -58), new Vector2(580, 2));

        // Row 1: Entered Run With
        var enteredRunLabel = CreateTMP("EnteredRunLabel", rightCardRT, "⚡  ENTERED RUN WITH", font, 15, new Color(0.9f, 0.95f, 1f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(enteredRunLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(45, -95), new Vector2(250, 24));

        Image enteredRunFill;
        CreateProgressBar(rightCardRT, "EnteredRunBar", new Vector2(-20, -125), new Vector2(380, 14), 0.31f, new Color(0f, 0.9f, 1f, 1f), out enteredRunFill);
        controller.enteredRunProgressBar = enteredRunFill;

        var enteredRunPercent = CreateTMP("EnteredRunPercent", rightCardRT, "31%", font, 22, new Color(0f, 0.9f, 1f, 1f), TextAlignmentOptions.Right, true, false);
        SetAnchors(enteredRunPercent.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-45, -125), new Vector2(80, 30));
        controller.enteredRunPercentText = enteredRunPercent;

        // Row 2: The Wall
        var wallLabel = CreateTMP("WallLabel", rightCardRT, "▲  THE WALL", font, 18, new Color(1f, 1f, 1f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(wallLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(45, -175), new Vector2(220, 24));

        var wallSub = CreateTMP("WallSub", rightCardRT, "Reached at 700m", font, 13, new Color(0.55f, 0.65f, 0.72f, 1f), TextAlignmentOptions.Left, false, true);
        SetAnchors(wallSub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(45, -198), new Vector2(220, 20));
        controller.theWallSubText = wallSub;

        // Pushed Through Button/Badge
        GameObject pushedThroughBtnObj = CreateButtonObj("PushedThroughBadge", rightCardRT, "PUSHED THROUGH", font, 14,
            new Color(0.20f, 0.15f, 0.05f, 0.9f),
            new Color(1f, 0.75f, 0.1f, 0.95f),
            new Color(1f, 0.84f, 0.31f, 1f),
            new Vector2(1, 1), new Vector2(-45, -185), new Vector2(210, 40));
        controller.pushedThroughBadge = pushedThroughBtnObj.GetComponent<Button>();

        // Row 3: Final Stamina
        var finalStaminaLabel = CreateTMP("FinalStaminaLabel", rightCardRT, "♥  FINAL STAMINA", font, 15, new Color(0.9f, 0.95f, 1f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(finalStaminaLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(45, -250), new Vector2(250, 24));

        Image finalStaminaFill;
        CreateProgressBar(rightCardRT, "FinalStaminaBar", new Vector2(-20, -280), new Vector2(380, 14), 0.12f, new Color(1f, 0.24f, 0f, 1f), out finalStaminaFill);
        controller.finalStaminaProgressBar = finalStaminaFill;

        var finalStaminaPercent = CreateTMP("FinalStaminaPercent", rightCardRT, "12%", font, 22, new Color(1f, 0.35f, 0.35f, 1f), TextAlignmentOptions.Right, true, false);
        SetAnchors(finalStaminaPercent.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-45, -280), new Vector2(80, 30));
        controller.finalStaminaPercentText = finalStaminaPercent;

        // ==========================================
        // 4. BOTTOM ACTION BUTTONS
        // ==========================================
        GameObject bottomBar = CreateEmpty("BottomBar", panelRT);
        RectTransform bottomRT = bottomBar.GetComponent<RectTransform>();
        SetAnchors(bottomRT, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 45), new Vector2(1200, 70));

        // Retry Button
        GameObject retryBtn = CreateButtonObj("RetryButton", bottomRT, "⟳  RETRY", font, 20,
            new Color(0.04f, 0.14f, 0.27f, 0.9f),
            new Color(0f, 0.7f, 1f, 0.7f),
            new Color(0.85f, 0.95f, 1f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(-380, 0), new Vector2(270, 56));
        controller.retryButton = retryBtn.GetComponent<Button>();

        // Main Menu Button
        GameObject menuBtn = CreateButtonObj("MainMenuButton", bottomRT, "⌂  MAIN MENU", font, 20,
            new Color(0.04f, 0.14f, 0.27f, 0.9f),
            new Color(0f, 0.7f, 1f, 0.7f),
            new Color(0.85f, 0.95f, 1f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(300, 56));
        controller.mainMenuButton = menuBtn.GetComponent<Button>();

        // Next Race Button
        GameObject nextBtn = CreateButtonObj("NextRaceButton", bottomRT, ">>  NEXT RACE", font, 20,
            new Color(0f, 0.45f, 0.75f, 0.95f),
            new Color(0f, 0.95f, 1f, 1f),
            Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(380, 0), new Vector2(320, 56));
        controller.nextRaceButton = nextBtn.GetComponent<Button>();

        // Save & dirty scene
        EditorUtility.SetDirty(resultPanelGO);
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);

        Debug.Log("[ResultPanelBuilder] Result Panel UI successfully built in UIScene!");
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

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static GameObject CreateCard(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject card = CreateEmpty(name, parent);
        RectTransform rt = card.GetComponent<RectTransform>();
        SetAnchors(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.025f, 0.08f, 0.15f, 0.85f); // Sci-fi dark glass

        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.65f, 0.95f, 0.5f);
        outline.effectDistance = new Vector2(2, 2);

        return card;
    }

    private static TextMeshProUGUI CreateSplitRow(Transform parent, string name, string label, string timeVal, TMP_FontAsset font, Color accentColor, float yPos)
    {
        GameObject row = CreateEmpty(name, parent);
        RectTransform rowRT = row.GetComponent<RectTransform>();
        SetAnchors(rowRT, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, yPos), new Vector2(580, 44));

        // Accent pill bar
        GameObject pill = CreateImage("AccentPill", rowRT, accentColor);
        RectTransform pillRT = pill.GetComponent<RectTransform>();
        SetAnchors(pillRT, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(6, 34));

        // Discipline Label
        var lbl = CreateTMP("Label", rowRT, label, font, 22, Color.white, TextAlignmentOptions.Left, true, false);
        SetAnchors(lbl.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(35, 0), new Vector2(180, 36));

        // Time Value
        var val = CreateTMP("Time", rowRT, timeVal, font, 24, Color.white, TextAlignmentOptions.Right, true, false);
        SetAnchors(val.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(220, 36));

        return val;
    }

    private static void CreateProgressBar(Transform parent, string name, Vector2 pos, Vector2 size, float fillAmount, Color fillColor, out Image fillImage)
    {
        GameObject bar = CreateEmpty(name, parent);
        RectTransform barRT = bar.GetComponent<RectTransform>();
        SetAnchors(barRT, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), pos + new Vector2(65, 0), size);

        // Track background
        Image track = bar.AddComponent<Image>();
        track.color = new Color(0.05f, 0.12f, 0.22f, 0.95f);
        Outline trackOutline = bar.AddComponent<Outline>();
        trackOutline.effectColor = new Color(0.1f, 0.35f, 0.55f, 0.5f);
        trackOutline.effectDistance = new Vector2(1, 1);

        // Fill
        GameObject fillObj = CreateEmpty("Fill", barRT);
        RectTransform fillRT = fillObj.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0, 0);
        fillRT.anchorMax = new Vector2(fillAmount, 1);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fillRT.pivot = new Vector2(0, 0.5f);

        fillImage = fillObj.AddComponent<Image>();
        fillImage.color = fillColor;
    }

    private static GameObject CreateButtonObj(string name, Transform parent, string label, TMP_FontAsset font, float fontSize,
        Color bgColor, Color borderColor, Color textColor, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject btnObj = CreateEmpty(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        SetAnchors(rt, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);

        Image bg = btnObj.AddComponent<Image>();
        bg.color = bgColor;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2, 2);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.95f, 0.98f, 1f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;

        var text = CreateTMP("Text", rt, label, font, fontSize, textColor, TextAlignmentOptions.Center, true, false);
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
