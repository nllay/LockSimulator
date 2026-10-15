using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace LockSimulator
{
    /// <summary>界面流程。物理规则在 PinTumblerSim，剖面绘制和拨针输入在 LockStage。</summary>
    public class LockSimulatorApp : MonoBehaviour
    {
        static readonly Color BgTop = new Color(0.075f, 0.115f, 0.185f);
        static readonly Color BgBottom = new Color(0.02f, 0.03f, 0.055f);
        static readonly Color Card = new Color(0.085f, 0.13f, 0.20f);
        static readonly Color CardLocked = new Color(0.055f, 0.08f, 0.125f);
        static readonly Color Chip = new Color(0.11f, 0.17f, 0.26f);
        static readonly Color TextMain = new Color(0.92f, 0.95f, 1f);
        static readonly Color TextMuted = new Color(0.55f, 0.66f, 0.79f);
        static readonly Color Brass = new Color(0.93f, 0.70f, 0.30f);
        static readonly Color Cyan = new Color(0.25f, 0.85f, 0.98f);
        static readonly Color Good = new Color(0.40f, 0.95f, 0.65f);
        static readonly Color Bad = new Color(1f, 0.42f, 0.38f);
        static readonly Color Ink = new Color(0.04f, 0.06f, 0.10f);

        const int HintPenalty = 120;
        const int ResetPenalty = 70;
        const int OversetPenalty = 50;
        const int BlindBonus = 150;

        class Bar
        {
            public RectTransform fill;
            public void Set(float v) { fill.anchorMax = new Vector2(Mathf.Clamp01(v), 1f); }
        }

        // 全局
        Camera backdrop;
        RectTransform root;
        CanvasScaler scaler;
        Vector2Int lastScreen;
        AudioSource sfx, scrapeSfx;
        AudioClip cSet, cFalse, cOver, cScrape, cReset, cCounter, cOpen, cButton;
        GameObject toastBox;
        Text toastText;
        float toastTimer;

        // 首页
        Transform homeScreen, listContent;
        Text homeProgress;

        // 拨锁
        Transform playScreen;
        LockSpec spec;
        PinTumblerSim sim;
        LockStage stage;
        bool running, xray = true, hintOn, usedXray;
        int hints;
        float elapsed, pendingResult;
        Text titleText, timerText, statusText, helpText;
        Button modeButton, hintButton;
        Bar resistBar, angleBar;
        GameObject resultPanel;
        Text resultTitle, resultBody;
        Image[] resultStars;
        Button resultPrimary, resultSecondary, resultTertiary;

        void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Application.targetFrameRate = 60;
            BuildCamera();
            BuildAudio();
            BuildUI();
            ShowHome();
        }

        void Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-shot") { StartCoroutine(ShotRoutine(args[i + 1])); break; }
        }

        // ---------------------------------------------------------------- 基础设施

        void BuildCamera()
        {
            var go = new GameObject("BackdropCamera");
            go.transform.SetParent(transform);
            backdrop = go.AddComponent<Camera>();
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = Color.Lerp(BgTop, BgBottom, 0.5f);
            backdrop.cullingMask = 0;
        }

        void BuildAudio()
        {
            sfx = NewSource("Sfx");
            scrapeSfx = NewSource("Scrape");
            scrapeSfx.volume = 0.5f;
            cSet = Sfx.Set(); cFalse = Sfx.FalseSet(); cOver = Sfx.Overset(); cScrape = Sfx.Scrape();
            cReset = Sfx.Reset(); cCounter = Sfx.Counter(); cOpen = Sfx.Open(); cButton = Sfx.Button();
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0.8f;
            return source;
        }

        void Play(AudioClip clip) { if (clip != null) sfx.PlayOneShot(clip); }

        static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Application.isMobilePlatform) Handheld.Vibrate();
#endif
        }

        void ShowToast(string message, Color color)
        {
            toastText.text = message;
            toastText.color = color;
            toastBox.SetActive(true);
            toastTimer = 2.2f;
        }

        // 窗口可任意缩放：内容保持 9:16，宽屏时左右留边。
        void ApplyLayout()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size == lastScreen || size.y == 0) return;
            lastScreen = size;
            float aspect = (float)size.x / size.y;
            const float target = 9f / 16f;
            float width = aspect > target ? target / aspect : 1f;
            float x0 = (1f - width) * 0.5f;
            root.anchorMin = new Vector2(x0, 0f);
            root.anchorMax = new Vector2(x0 + width, 1f);
            scaler.matchWidthOrHeight = aspect > target ? 1f : 0f;
        }

        // ---------------------------------------------------------------- UI 构建

        void BuildUI()
        {
            var canvasObject = new GameObject("UI");
            canvasObject.transform.SetParent(transform);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem(canvasObject.transform);

            root = UiKit.Rect("Root", canvas.transform, 0f, 0f, 1f, 1f);
            UiKit.Picture(root, "Background", Art.VGrad(BgTop, BgBottom), Color.white);
            homeScreen = UiKit.Rect("Home", root, 0f, 0f, 1f, 1f);
            playScreen = UiKit.Rect("Play", root, 0f, 0f, 1f, 1f);
            BuildHome();
            BuildPlay();
            playScreen.gameObject.SetActive(false);

            toastBox = UiKit.Panel(root, "Toast", new Color(0.02f, 0.04f, 0.08f, 0.94f), 0.1f, 0.895f, 0.9f, 0.945f).gameObject;
            toastText = UiKit.Label(toastBox.transform, "Text", "", 22, TextAnchor.MiddleCenter, TextMuted);
            toastBox.SetActive(false);
        }

        // 项目只启用了新输入系统，旧的 StandaloneInputModule 读不到输入。
        void EnsureEventSystem(Transform parent)
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildHome()
        {
            var s = homeScreen;

            // 挂锁徽标
            var icon = UiKit.FitBox(s, "Emblem", 0.25f, 0.715f, 0.75f, 0.955f, 1f);
            UiKit.Picture(icon, "Glow", Art.Glow, new Color(Brass.r, Brass.g, Brass.b, 0.28f), 0f, 0f, 1f, 1f);
            UiKit.Picture(icon, "Shackle", Art.Ring, new Color(0.72f, 0.77f, 0.86f), 0.27f, 0.36f, 0.73f, 0.88f);
            UiKit.Picture(icon, "ShackleCore", Art.Ring, new Color(0.35f, 0.4f, 0.5f), 0.34f, 0.43f, 0.66f, 0.81f);
            UiKit.Panel(icon, "Body", Brass, 0.16f, 0.10f, 0.84f, 0.55f);
            UiKit.Picture(icon, "BodyShade", Art.VGrad(new Color(1, 1, 1, 0.22f), new Color(0, 0, 0, 0.35f)), Color.white, 0.16f, 0.10f, 0.84f, 0.55f);
            UiKit.Picture(icon, "Keyhole", Art.Keyhole, Ink, 0.39f, 0.15f, 0.61f, 0.48f);

            UiKit.Label(s, "Kicker", "LOCK LAB  /  PIN TUMBLER", 18, TextAnchor.MiddleCenter, Cyan, 0.08f, 0.685f, 0.92f, 0.72f);
            UiKit.Label(s, "Title", "开锁模拟器", 60, TextAnchor.MiddleCenter, TextMain, 0.08f, 0.62f, 0.92f, 0.69f);
            UiKit.Label(s, "Subtitle", "写实剖面 · 真实的弹子锁物理", 24, TextAnchor.MiddleCenter, TextMuted, 0.08f, 0.585f, 0.92f, 0.625f);

            UiKit.Label(s, "ListLabel", "选择锁具", 28, TextAnchor.MiddleLeft, Brass, 0.07f, 0.525f, 0.5f, 0.57f);
            homeProgress = UiKit.Label(s, "Progress", "", 22, TextAnchor.MiddleRight, TextMuted, 0.5f, 0.525f, 0.93f, 0.57f);

            var scroll = UiKit.Rect("Scroll", s, 0.05f, 0.045f, 0.95f, 0.515f);
            var viewport = UiKit.Rect("Viewport", scroll, 0f, 0f, 1f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();
            UiKit.Picture(viewport, "Hit", Art.Flat, new Color(0, 0, 0, 0), 0f, 0f, 1f, 1f, false, true);
            var content = UiKit.Rect("Content", viewport, 0f, 1f, 1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            listContent = content;

            UiKit.Label(s, "Footer", "单机 · 本地进度 · 无需网络", 20, TextAnchor.MiddleCenter, TextMuted, 0.08f, 0.01f, 0.92f, 0.045f);
        }

        void RefreshList()
        {
            UiKit.Clear(listContent);
            int done = 0;
            foreach (var spec in LockCatalog.All)
            {
                if (ProgressStore.IsDone(spec.id)) done++;
                AddCard(spec);
            }
            homeProgress.text = "已完成 " + done + " / " + LockCatalog.All.Count;
        }

        void AddCard(LockSpec s)
        {
            bool unlocked = ProgressStore.IsUnlocked(s.id);
            var button = UiKit.Button(listContent, "Lock" + s.id, "", unlocked ? Card : CardLocked, TextMain, 20);
            UiKit.ButtonLabel(button).text = "";
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 170f;
            var t = button.transform;

            // 弹子示意：n 根竖条
            Color accent = unlocked ? s.accent : new Color(0.25f, 0.3f, 0.38f);
            for (int i = 0; i < s.pins; i++)
            {
                float x = 0.045f + i * 0.032f;
                float h = 0.28f + 0.1f * ((i * 37 + s.id * 11) % 4);
                UiKit.Panel(t, "Pin" + i, accent, x, 0.5f - h * 0.5f, x + 0.02f, 0.5f + h * 0.5f, false);
            }
            UiKit.Label(t, "Name", (unlocked ? "" : "未解锁  ") + s.name, 32, TextAnchor.MiddleLeft, unlocked ? TextMain : TextMuted, 0.28f, 0.52f, 0.78f, 0.92f);
            UiKit.Label(t, "Note", unlocked ? s.note : "完成上一把锁后解锁", 19, TextAnchor.MiddleLeft, TextMuted, 0.28f, 0.10f, 0.78f, 0.52f);

            int stars = ProgressStore.Stars(s.id);
            var starBox = UiKit.FitBox(t, "Stars", 0.78f, 0.48f, 0.97f, 0.88f, 3f);
            for (int i = 0; i < 3; i++)
                UiKit.Picture(starBox, "Star" + i, Art.Star, i < stars ? Brass : new Color(0.2f, 0.26f, 0.35f), i / 3f + 0.02f, 0.05f, (i + 1) / 3f - 0.02f, 0.95f);
            if (ProgressStore.Best(s.id) > 0)
                UiKit.Label(t, "Best", "最佳 " + ProgressStore.Best(s.id), 18, TextAnchor.MiddleRight, Brass, 0.62f, 0.10f, 0.96f, 0.45f);

            button.interactable = unlocked;
            button.onClick.AddListener(() => { Play(cButton); StartLock(s); });
        }

        void BuildPlay()
        {
            var s = playScreen;
            var back = UiKit.Button(s, "Back", "< 返回", Chip, TextMain, 24, 0.04f, 0.935f, 0.25f, 0.985f);
            back.onClick.AddListener(() => { Play(cButton); ShowHome(); });
            titleText = UiKit.Label(s, "Title", "", 30, TextAnchor.MiddleCenter, TextMain, 0.27f, 0.935f, 0.73f, 0.985f);
            timerText = UiKit.Label(s, "Timer", "", 28, TextAnchor.MiddleRight, TextMuted, 0.74f, 0.935f, 0.96f, 0.985f);

            resistBar = MakeBar(s, "拨针阻力", Brass, 0.06f, 0.885f, 0.48f, 0.93f);
            angleBar = MakeBar(s, "锁芯转角", Cyan, 0.52f, 0.885f, 0.94f, 0.93f);

            var host = UiKit.FitBox(s, "Stage", 0.03f, 0.26f, 0.97f, 0.88f, LockStage.W / LockStage.H);
            stage = new LockStage(host);
            stage.TensionChanged = t => { if (running) sim.SetTension(t); };
            stage.PickChanged = (lane, p) => { if (running) sim.Pick(lane, p); };
            stage.PickEnded = () => { if (sim != null) sim.Release(); };

            modeButton = UiKit.Button(s, "Mode", "", Chip, TextMain, 24, 0.05f, 0.175f, 0.34f, 0.245f);
            modeButton.onClick.AddListener(ToggleMode);
            hintButton = UiKit.Button(s, "Hint", "显示卡位  -" + HintPenalty, Chip, TextMain, 22, 0.355f, 0.175f, 0.645f, 0.245f);
            hintButton.onClick.AddListener(UseHint);
            var ease = UiKit.Button(s, "Ease", "松开张力", Chip, TextMain, 24, 0.66f, 0.175f, 0.95f, 0.245f);
            ease.onClick.AddListener(() => { if (running) { sim.SetTension(0f); Play(cButton); } });

            UiKit.Panel(s, "StatusBg", new Color(0.04f, 0.07f, 0.12f), 0.05f, 0.085f, 0.95f, 0.165f);
            statusText = UiKit.Label(s, "Status", "", 22, TextAnchor.MiddleCenter, TextMuted, 0.07f, 0.085f, 0.93f, 0.165f);
            helpText = UiKit.Label(s, "Help", "拨针区：按住某一列向上拖动。张力条拖到哪里就保持在哪里。\n桌面端：滚轮 / ↑↓ 微调张力。", 17, TextAnchor.MiddleCenter, TextMuted, 0.05f, 0.015f, 0.95f, 0.08f);

            BuildResult(s);
        }

        Bar MakeBar(Transform parent, string title, Color color, float x0, float y0, float x1, float y1)
        {
            var holder = UiKit.Rect(title, parent, x0, y0, x1, y1);
            UiKit.Label(holder, "Label", title, 18, TextAnchor.MiddleLeft, TextMuted, 0f, 0.5f, 1f, 1f);
            var track = UiKit.Panel(holder, "Track", new Color(0.03f, 0.055f, 0.10f), 0f, 0.1f, 1f, 0.42f, false);
            var fill = UiKit.Panel(track, "Fill", color, 0f, 0f, 0f, 1f, false);
            return new Bar { fill = fill };
        }

        void BuildResult(Transform parent)
        {
            var dim = UiKit.Panel(parent, "Result", new Color(0f, 0f, 0f, 0.62f), 0f, 0f, 1f, 1f, false);
            dim.GetComponent<Image>().raycastTarget = true;
            resultPanel = dim.gameObject;
            var card = UiKit.Panel(dim, "Card", new Color(0.07f, 0.11f, 0.18f), 0.07f, 0.2f, 0.93f, 0.8f);
            resultTitle = UiKit.Label(card, "Title", "", 50, TextAnchor.MiddleCenter, Good, 0.05f, 0.80f, 0.95f, 0.96f);
            var starBox = UiKit.FitBox(card, "Stars", 0.25f, 0.65f, 0.75f, 0.80f, 3f);
            resultStars = new Image[3];
            for (int i = 0; i < 3; i++)
                resultStars[i] = UiKit.Picture(starBox, "Star" + i, Art.Star, Brass, i / 3f + 0.02f, 0.05f, (i + 1) / 3f - 0.02f, 0.95f);
            resultBody = UiKit.Label(card, "Body", "", 26, TextAnchor.MiddleCenter, TextMain, 0.06f, 0.37f, 0.94f, 0.64f);
            resultPrimary = UiKit.Button(card, "Primary", "", Brass, Ink, 30, 0.1f, 0.23f, 0.9f, 0.34f);
            resultSecondary = UiKit.Button(card, "Secondary", "", Chip, TextMain, 26, 0.1f, 0.12f, 0.9f, 0.21f);
            resultTertiary = UiKit.Button(card, "Tertiary", "", Chip, TextMain, 26, 0.1f, 0.02f, 0.9f, 0.10f);
            resultPanel.SetActive(false);
        }

        // ---------------------------------------------------------------- 流程

        void ShowHome()
        {
            running = false;
            playScreen.gameObject.SetActive(false);
            homeScreen.gameObject.SetActive(true);
            RefreshList();
        }

        void StartLock(LockSpec s)
        {
            spec = s;
            int seed = s.id * 7919 + ProgressStore.NextRun(s.id) * 104729;
            if (sim != null) sim.Fired -= OnSimEvent;
            sim = new PinTumblerSim(s, seed);
            sim.Fired += OnSimEvent;
            stage.Rebuild(s.pins);
            elapsed = 0f; hints = 0; pendingResult = 0f;
            hintOn = false; usedXray = false;
            running = true;
            resultPanel.SetActive(false);
            titleText.text = s.name;
            RefreshModeLabels();
            SetStatus("先把张力条拖到中间，再按住拨针区的一列向上拖动。", TextMuted);
            homeScreen.gameObject.SetActive(false);
            playScreen.gameObject.SetActive(true);
        }

        void RefreshModeLabels()
        {
            UiKit.ButtonLabel(modeButton).text = xray ? "模式：透视" : "模式：盲拨";
            UiKit.ButtonLabel(hintButton).text = hintOn ? "卡位提示：开" : "显示卡位  -" + HintPenalty;
        }

        void ToggleMode()
        {
            xray = !xray;
            Play(cButton);
            RefreshModeLabels();
            SetStatus(xray ? "透视模式：可以看到内部结构。" : "盲拨模式：看不到内部，靠手感、声音和锁芯转角判断。", TextMuted);
        }

        void UseHint()
        {
            if (!running) return;
            if (!xray) { ShowToast("盲拨模式下没有提示", Bad); return; }
            Play(cButton);
            if (!hintOn) { hintOn = true; hints++; }
            else hintOn = false;
            RefreshModeLabels();
        }

        void SetStatus(string message, Color color)
        {
            statusText.text = message;
            statusText.color = color;
        }

        void OnSimEvent(SimEvent ev, int index)
        {
            switch (ev)
            {
                case SimEvent.Set:
                    usedXray |= xray;
                    Play(cSet);
                    SetStatus("咔哒——一根弹子被设定（" + sim.SetCount + " / " + sim.pins.Length + "）", Good);
                    break;
                case SimEvent.FalseSet:
                    usedXray |= xray;
                    Play(cFalse);
                    SetStatus("锁芯突然多转了一截——可能是假设定。放松一点张力，再继续抬。", Brass);
                    break;
                case SimEvent.Overset:
                    Play(cOver);
                    SetStatus("抬过头了，弹子卡死。把张力放松到很小，让它回落。", Bad);
                    break;
                case SimEvent.Free:
                    Play(cScrape);
                    SetStatus("弹子回落了。", TextMuted);
                    break;
                case SimEvent.Reset:
                    Play(cReset);
                    SetStatus("张力松开，所有弹子弹回原位，重新开始。", Bad);
                    break;
                case SimEvent.CounterRotate:
                    Play(cCounter);
                    SetStatus("锁芯在回弹，顶住了弹子：张力太大，放松一点。", Brass);
                    break;
                case SimEvent.Scrape:
                    scrapeSfx.pitch = Random.Range(0.85f, 1.25f);
                    scrapeSfx.PlayOneShot(cScrape);
                    break;
                case SimEvent.Open:
                    Complete();
                    break;
            }
        }

        void Complete()
        {
            running = false;
            Play(cOpen);
            Vibrate();
            bool blind = !usedXray;
            int late = Mathf.Min(400, Mathf.RoundToInt(Mathf.Max(0f, elapsed - spec.par) * 2f));
            int score = 1000 - late - sim.resets * ResetPenalty - sim.oversets * OversetPenalty - hints * HintPenalty + (blind ? BlindBonus : 0);
            score = Mathf.Max(100, score);
            int stars = score >= 800 ? 3 : score >= 500 ? 2 : 1;
            bool record = ProgressStore.Record(spec.id, score, stars);

            resultTitle.text = "锁已打开";
            for (int i = 0; i < 3; i++) resultStars[i].color = i < stars ? Brass : new Color(0.2f, 0.26f, 0.35f);
            var body = new StringBuilder();
            body.Append("用时 ").Append(elapsed.ToString("0.0")).Append(" 秒（参考 ").Append(Mathf.RoundToInt(spec.par)).AppendLine(" 秒）");
            body.Append("重置 ").Append(sim.resets).Append(" 次 · 过推 ").Append(sim.oversets).Append(" 次 · 提示 ").Append(hints).AppendLine(" 次");
            body.Append("评分 ").Append(score).Append(record ? "  ·  新纪录" : "").Append(blind ? "\n盲拨完成，额外 +" + BlindBonus : "");
            resultBody.text = body.ToString();

            bool hasNext = spec.id < LockCatalog.All.Count;
            var next = hasNext ? LockCatalog.All[spec.id] : null;
            SetResultButton(resultPrimary, hasNext ? "下一把：" + next.name : "返回锁具列表", hasNext ? (UnityEngine.Events.UnityAction)(() => StartLock(next)) : ShowHome);
            var again = spec;
            SetResultButton(resultSecondary, "再来一把（换一把新锁）", () => StartLock(again));
            SetResultButton(resultTertiary, "返回锁具列表", ShowHome);
            pendingResult = 1.1f;
        }

        void SetResultButton(Button button, string label, UnityEngine.Events.UnityAction action)
        {
            UiKit.ButtonLabel(button).text = label;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { Play(cButton); action(); });
        }

        // ---------------------------------------------------------------- 帧更新

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            if (toastTimer > 0f)
            {
                toastTimer -= dt;
                if (toastTimer <= 0f) toastBox.SetActive(false);
            }
            ApplyLayout();
            if (!playScreen.gameObject.activeSelf || sim == null) return;

            if (running)
            {
                elapsed += dt;
                ReadDesktopInput(dt);
            }
            sim.Tick(dt);
            stage.Refresh(sim, xray, hintOn, dt, Time.unscaledTime);
            resistBar.Set(sim.resist);
            angleBar.Set(sim.angle);
            timerText.text = FormatTime(elapsed);

            if (pendingResult > 0f)
            {
                pendingResult -= dt;
                if (pendingResult <= 0f) resultPanel.SetActive(true);
            }
        }

        static string FormatTime(float t)
        {
            int total = Mathf.FloorToInt(t);
            return (total / 60).ToString("0") + ":" + (total % 60).ToString("00");
        }

        // 鼠标没有第二根手指：滚轮 / 方向键微调张力，配合拖动的张力条使用。
        void ReadDesktopInput(float dt)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float d = 0f;
                if (keyboard.upArrowKey.isPressed || keyboard.rightArrowKey.isPressed) d += 1f;
                if (keyboard.downArrowKey.isPressed || keyboard.leftArrowKey.isPressed) d -= 1f;
                if (d != 0f) sim.SetTension(sim.tension + d * 0.4f * dt);
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f) sim.SetTension(sim.tension + Mathf.Clamp(scroll * 0.01f, -0.04f, 0.04f));
            }
#endif
        }

        // ---------------------------------------------------------------- 截图自检（命令行 -shot <目录>）

        IEnumerator ShotRoutine(string dir)
        {
            ProgressStore.ReadOnly = true;
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(0.8f);
            yield return Capture(dir, "1-home");

            Debug.Log("[shot] start lock");
            StartLock(LockCatalog.All[3]);
            Debug.Log("[shot] lock started");
            yield return new WaitForSeconds(0.5f);
            SimBot.Solve(sim, 2);
            sim.SetTension(0.3f);
            int bind = sim.BindingIndex();
            sim.Pick(bind, sim.pins[bind].setH * 0.55f);
            yield return new WaitForSeconds(1.2f);
            yield return Capture(dir, "2-play");

            hintOn = true;
            yield return new WaitForSeconds(0.3f);
            yield return Capture(dir, "3-hint");
            hintOn = false;

            ToggleMode();
            yield return new WaitForSeconds(0.4f);
            yield return Capture(dir, "4-blind");

            ToggleMode();
            sim.Release();
            SimBot.Solve(sim);
            yield return new WaitForSeconds(2f);
            yield return Capture(dir, "5-result");
            Application.Quit();
        }

        IEnumerator Capture(string dir, string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return new WaitForSeconds(0.7f);
        }
    }
}
