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
        if (!SessionState.GetBool("ResultPanelAutoBuilt_v4_perf", false))
        {
            SessionState.SetBool("ResultPanelAutoBuilt_v4_perf", true);
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

        // Stretch full-screen
        RectTransform panelRT = resultPanelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        panelRT.pivot = new Vector2(0.5f, 0.5f);

        Image panelImg = resultPanelGO.GetComponent<Image>();
        if (panelImg != null && panelImg.sprite == null)
        {
            panelImg.color = new Color(0.02f, 0.05f, 0.10f, 0.88f);
        }

        // Clean out previous children
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
        // 1. TOP HEADER SECTION (Outcome & Title)
        // ==========================================
        GameObject headerGroup = CreateEmpty("HeaderGroup", panelRT);
        RectTransform headerRT = headerGroup.GetComponent<RectTransform>();
        SetAnchors(headerRT, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(0, 180));

        // Subheader: /// RACE COMPLETE
        var raceComplete = CreateTMP("RaceCompleteText", headerRT, "///  RACE COMPLETE", font, 24, new Color(0f, 0.9f, 1f, 1f), TextAlignmentOptions.Left, true, true);
        SetAnchors(raceComplete.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -50), new Vector2(400, 40));
        controller.raceCompleteText = raceComplete;

        // Title: VICTORY / DEFEAT
        var victory = CreateTMP("VictoryText", headerRT, "VICTORY", font, 78, new Color(1f, 0.84f, 0.31f, 1f), TextAlignmentOptions.Center, true, true);
        SetAnchors(victory.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -55), new Vector2(600, 95));
        controller.outcomeTitleText = victory;

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
        controller.newBestBadge = newBestObj;
        controller.newBestText = newBestText;

        // ==========================================
        // 2. CENTER TIME DISPLAY BOX (Your Total Time vs AI Time)
        // ==========================================
        GameObject timeBox = CreateEmpty("CenterTimeBox", panelRT);
        RectTransform timeBoxRT = timeBox.GetComponent<RectTransform>();
        SetAnchors(timeBoxRT, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(740, 90));
        var timeBoxBg = timeBox.AddComponent<Image>();
        timeBoxBg.color = new Color(0.03f, 0.09f, 0.17f, 0.90f);
        var timeBoxOutline = timeBox.AddComponent<Outline>();
        timeBoxOutline.effectColor = new Color(0f, 0.7f, 1f, 0.45f);
        timeBoxOutline.effectDistance = new Vector2(2, 2);

        // Your Total Time (Left Column)
        GameObject yourTimeCol = CreateEmpty("YourTimeCol", timeBoxRT);
        RectTransform yourTimeRT = yourTimeCol.GetComponent<RectTransform>();
        SetAnchors(yourTimeRT, new Vector2(0.25f, 0.5f), new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 80));

        var yourTimeLabel = CreateTMP("Label", yourTimeRT, "YOUR TOTAL TIME", font, 13, new Color(0.5f, 0.85f, 1f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(yourTimeLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(300, 22));

        var yourTimeVal = CreateTMP("Value", yourTimeRT, "04:32.50", font, 42, new Color(0f, 0.96f, 1f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(yourTimeVal.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(300, 48));
        controller.yourTotalTimeValue = yourTimeVal;

        // Divider
        GameObject centerDivider = CreateImage("Divider", timeBoxRT, new Color(0.15f, 0.35f, 0.55f, 0.5f));
        RectTransform divRT = centerDivider.GetComponent<RectTransform>();
        SetAnchors(divRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 58));

        // AI Time (Right Column)
        GameObject aiTimeCol = CreateEmpty("AITimeCol", timeBoxRT);
        RectTransform aiTimeRT = aiTimeCol.GetComponent<RectTransform>();
        SetAnchors(aiTimeRT, new Vector2(0.75f, 0.5f), new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 80));

        var aiTimeLabel = CreateTMP("Label", aiTimeRT, "AI RIVAL TIME", font, 13, new Color(0.6f, 0.7f, 0.75f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(aiTimeLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(300, 22));

        var aiTimeVal = CreateTMP("Value", aiTimeRT, "04:41.20", font, 36, new Color(0.92f, 0.94f, 0.96f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(aiTimeVal.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(300, 40));
        controller.aiTimeValue = aiTimeVal;

        var timeDiff = CreateTMP("TimeDiff", aiTimeRT, "-08.70s AHEAD", font, 12, new Color(0f, 0.95f, 0.45f, 1f), TextAlignmentOptions.Center, true, false);
        SetAnchors(timeDiff.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(300, 16));
        controller.timeDiffText = timeDiff;

        // ==========================================
        // 3. TWO CARDS: STAGE SPLITS & PERFORMANCE SUMMARY
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

        // Splits Footer
        var splitsFooterLabel = CreateTMP("SplitsFooterLabel", leftCardRT, "TOTAL STAGE TIME", font, 15, new Color(0.55f, 0.65f, 0.7f, 1f), TextAlignmentOptions.Left, false, false);
        SetAnchors(splitsFooterLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(55, -315), new Vector2(240, 30));

        var splitsFooterVal = CreateTMP("SplitsFooterValue", leftCardRT, "04:32.50", font, 20, new Color(0.8f, 0.9f, 1f, 1f), TextAlignmentOptions.Right, true, false);
        SetAnchors(splitsFooterVal.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-55, -315), new Vector2(200, 30));
        controller.stageSplitsFooterText = splitsFooterVal;

        // --- RIGHT CARD: PERFORMANCE SUMMARY ---
        GameObject rightCard = CreateCard("RightCard_PerformanceSummary", panelRT, new Vector2(350, cardY), new Vector2(cardW, cardH));
        RectTransform rightCardRT = rightCard.GetComponent<RectTransform>();

        // Header Title
        var perfTitle = CreateTMP("Title", rightCardRT, ">  PERFORMANCE SUMMARY", font, 20, new Color(0.1f, 0.9f, 1f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(perfTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -25), new Vector2(400, 30));

        GameObject perfLine = CreateImage("Separator", rightCardRT, new Color(0.12f, 0.28f, 0.48f, 0.7f));
        SetAnchors(perfLine.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -58), new Vector2(580, 2));

        // 1. Stamina Remaining (Single percentage with progress bar)
        var staminaLabel = CreateTMP("StaminaLabel", rightCardRT, "⚡  STAMINA REMAINING", font, 15, new Color(0.9f, 0.95f, 1f, 1f), TextAlignmentOptions.Left, true, false);
        SetAnchors(staminaLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(45, -92), new Vector2(260, 24));

        Image staminaFill;
        CreateProgressBar(rightCardRT, "StaminaBar", new Vector2(-20, -122), new Vector2(380, 14), 0.68f, new Color(0f, 0.92f, 1f, 1f), out staminaFill);
        controller.staminaProgressBar = staminaFill;

        var staminaPercent = CreateTMP("StaminaPercent", rightCardRT, "68%", font, 22, new Color(0f, 0.92f, 1f, 1f), TextAlignmentOptions.Right, true, false);
        SetAnchors(staminaPercent.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-45, -122), new Vector2(80, 30));
        controller.staminaPercentText = staminaPercent;

        // 2. Total Score
        GameObject scoreRow = CreateEmpty("ScoreRow", rightCardRT);
        RectTransform scoreRT = scoreRow.GetComponent<RectTransform>();
        SetAnchors(scoreRT, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -172), new Vector2(580, 40));

        GameObject scorePill = CreateImage("Pill", scoreRT, new Color(0f, 0.85f, 1f, 1f));
        SetAnchors(scorePill.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(6, 30));

        var scoreLabel = CreateTMP("Label", scoreRT, "TOTAL SCORE", font, 18, Color.white, TextAlignmentOptions.Left, true, false);
        SetAnchors(scoreLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(200, 30));

        var scoreVal = CreateTMP("Value", scoreRT, "2,850 PTS", font, 24, new Color(0f, 0.96f, 1f, 1f), TextAlignmentOptions.Right, true, false);
        SetAnchors(scoreVal.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(250, 30));
        controller.totalScoreText = scoreVal;

        // 3. Coins Earned
        GameObject coinsRow = CreateEmpty("CoinsRow", rightCardRT);
        RectTransform coinsRT = coinsRow.GetComponent<RectTransform>();
        SetAnchors(coinsRT, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -225), new Vector2(580, 40));

        GameObject coinsPill = CreateImage("Pill", coinsRT, new Color(1f, 0.78f, 0.15f, 1f));
        SetAnchors(coinsPill.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(6, 30));

        var coinsLabel = CreateTMP("Label", coinsRT, "COINS EARNED", font, 18, Color.white, TextAlignmentOptions.Left, true, false);
        SetAnchors(coinsLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(200, 30));

        var coinsVal = CreateTMP("Value", coinsRT, "+150 COINS", font, 24, new Color(1f, 0.84f, 0.31f, 1f), TextAlignmentOptions.Right, true, false);
        SetAnchors(coinsVal.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(250, 30));
        controller.coinsEarnedText = coinsVal;

        // Footer separator
        GameObject perfFooterLine = CreateImage("FooterSeparator", rightCardRT, new Color(0.12f, 0.28f, 0.48f, 0.4f));
        SetAnchors(perfFooterLine.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -280), new Vector2(580, 1));

        // Outcome Status footer
        var outcomeStatusLabel = CreateTMP("OutcomeStatusLabel", rightCardRT, "RACE OUTCOME", font, 15, new Color(0.55f, 0.65f, 0.7f, 1f), TextAlignmentOptions.Left, false, false);
        SetAnchors(outcomeStatusLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(55, -315), new Vector2(200, 30));

        var outcomeStatusVal = CreateTMP("OutcomeStatusValue", rightCardRT, "VICTORY", font, 20, new Color(0f, 0.95f, 0.45f, 1f), TextAlignmentOptions.Right, true, false);
        SetAnchors(outcomeStatusVal.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-55, -315), new Vector2(200, 30));
        controller.outcomeStatusText = outcomeStatusVal;

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

        Debug.Log("[ResultPanelBuilder] Result Panel UI successfully rebuilt with Performance Summary!");
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
