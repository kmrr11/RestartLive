using System.Collections.Generic;
using System.Text;
using LifeSim.Core;
using LifeSim.Data;
using UnityEngine;
using UnityEngine.UI;

namespace LifeSim.UI
{
    public sealed class GameUI : MonoBehaviour
    {
        [SerializeField] TextAsset eventsCsv;
        [SerializeField] TextAsset branchesCsv;
        [SerializeField] TextAsset storiesCsv;
        [SerializeField] TextAsset storyStepsCsv;
        [SerializeField] TextAsset buffsCsv;
        [SerializeField] TextAsset charactersCsv;

        GameSession _session;
        readonly StringBuilder _logBuilder = new StringBuilder();

        // Panels
        GameObject _allocatePanel;
        GameObject _playPanel;
        GameObject _choicePanel;
        GameObject _endPanel;

        // Allocate
        Text _pointsText;
        readonly Dictionary<string, Text> _allocValueTexts = new Dictionary<string, Text>();

        // Play
        Text _statsText;
        Text _logText;
        ScrollRect _logScroll;
        Button _nextYearButton;
        Text _nextYearButtonLabel;
        Button _confessButton;

        // Choice
        Text _choicePrompt;
        Transform _choiceButtonRoot;
        readonly List<Button> _choiceButtons = new List<Button>();

        // End
        Text _summaryText;
        bool _reviewingLife;

        void Awake()
        {
            if (eventsCsv == null)
                eventsCsv = Resources.Load<TextAsset>("Data/Events");
            if (branchesCsv == null)
                branchesCsv = Resources.Load<TextAsset>("Data/Branches");
            if (storiesCsv == null)
                storiesCsv = Resources.Load<TextAsset>("Data/Stories");
            if (storyStepsCsv == null)
                storyStepsCsv = Resources.Load<TextAsset>("Data/StorySteps");
            if (buffsCsv == null)
                buffsCsv = Resources.Load<TextAsset>("Data/Buffs");
            if (charactersCsv == null)
                charactersCsv = Resources.Load<TextAsset>("Data/Characters");

            BuildUi();
            _session = new GameSession();
            _session.OnLog += AppendLog;
            _session.OnStateChanged += RefreshAll;
            _session.OnAwaitingChoice += ShowChoices;
            _session.OnEnded += ShowEnding;

            if (eventsCsv == null || branchesCsv == null)
            {
                Debug.LogError("[LifeSim] Missing Resources/Data/Events.txt or Branches.txt. Menu: LifeSim/Sync CSV To Resources");
                _session.ResetToAllocate();
                AppendLog("数据表缺失：请先执行菜单 LifeSim/Sync CSV To Resources");
                RefreshAll();
                return;
            }

            _session.Initialize(eventsCsv, branchesCsv, storiesCsv, storyStepsCsv, buffsCsv, charactersCsv);
            RefreshAll();
        }

        void AppendLog(string line)
        {
            if (_logBuilder.Length > 0)
                _logBuilder.AppendLine();
            _logBuilder.Append(line);
            if (_logText != null)
                _logText.text = _logBuilder.ToString();

            ScrollLogToBottom();
        }

        void ScrollLogToBottom()
        {
            if (_logScroll == null)
                return;

            Canvas.ForceUpdateCanvases();
            if (_logText != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_logText.rectTransform);
            if (_logScroll.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_logScroll.content);
            _logScroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }

        void RefreshAll()
        {
            if (_session == null || _session.Player == null)
                return;

            bool allocate = _session.Phase == GamePhase.Allocate;
            bool playing = _session.Phase == GamePhase.Playing ||
                           _session.Phase == GamePhase.AwaitingChoice ||
                           _session.Phase == GamePhase.InStory ||
                           _session.Phase == GamePhase.StoryAwaitingChoice;
            bool ended = _session.Phase == GamePhase.Ended;
            bool choosing = _session.Phase == GamePhase.AwaitingChoice ||
                            _session.Phase == GamePhase.StoryAwaitingChoice;

            _allocatePanel.SetActive(allocate);
            _playPanel.SetActive(playing || ended);
            _choicePanel.SetActive(choosing);
            _endPanel.SetActive(ended && !_reviewingLife);

            if (allocate)
            {
                var ap = _session.Player;
                _pointsText.text =
                    $"随机天赋（总和 {GameSession.PointPool}）\n力量 {ap.Strength}  智力 {ap.Intelligence}  运气 {ap.Luck}  家境 {ap.Family}";
                SetAllocLabel("str", ap.Strength);
                SetAllocLabel("int", ap.Intelligence);
                SetAllocLabel("luck", ap.Luck);
                SetAllocLabel("family", ap.Family);
            }

            var p = _session.Player;
            string storyHint = _session.InStoryMode ? "  [剧情中]" : string.Empty;
            string buffLine = p.FormatBuffs();
            string favorLine = p.FormatFavors();
            string confessHint = string.IsNullOrEmpty(p.PendingConfessId)
                ? string.Empty
                : (string.IsNullOrEmpty(p.PendingConfessName)
                    ? "\n已决定下一季告白"
                    : $"\n已决定下一季向{p.PendingConfessName}告白");
            _statsText.text =
                $"年龄 {p.Age} · {SeasonUtil.ToDisplay(p.Season)}{storyHint}\n" +
                $"力量 {p.GetAttr("str")}  智力 {p.GetAttr("int")}  运气 {p.GetAttr("luck")}  家境 {p.GetAttr("family")}" +
                (string.IsNullOrEmpty(favorLine) ? string.Empty : "\n" + favorLine) +
                (string.IsNullOrEmpty(buffLine) ? string.Empty : "\n" + buffLine) +
                confessHint;

            bool canAdvance = (_session.Phase == GamePhase.Playing ||
                               _session.Phase == GamePhase.InStory ||
                               _session.AwaitingContinue) && p.Alive;
            if (_nextYearButton != null)
                _nextYearButton.interactable = canAdvance || (ended && _reviewingLife);
            if (_confessButton != null)
            {
                _confessButton.gameObject.SetActive(!ended);
                _confessButton.interactable = _session.CanConfess;
            }
            if (_nextYearButtonLabel != null)
            {
                if (ended)
                    _nextYearButtonLabel.text = "返回结算";
                else if (_session.AwaitingContinue || _session.InStoryMode)
                    _nextYearButtonLabel.text = "继续";
                else
                    _nextYearButtonLabel.text = "下一季";
            }

            if (ended)
                _summaryText.text = _session.BuildSummary();
        }

        void SetAllocLabel(string key, int value)
        {
            if (_allocValueTexts.TryGetValue(key, out var text))
                text.text = value.ToString();
        }

        void ShowChoices()
        {
            foreach (var btn in _choiceButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }

            _choiceButtons.Clear();
            _choicePrompt.text = !string.IsNullOrEmpty(_session.ConfessPrompt)
                ? _session.ConfessPrompt
                : (_session.PendingStoryStep != null
                    ? _session.PendingStoryStep.Text
                    : (_session.PendingEvent != null ? _session.PendingEvent.Text : "做出你的选择"));

            foreach (var branch in _session.PendingChoices)
            {
                var captured = branch.ChoiceId;
                var btn = CreateButton(_choiceButtonRoot, branch.Label, () => _session.Choose(captured));
                _choiceButtons.Add(btn);
            }

            RefreshAll();
        }

        void ShowEnding()
        {
            _reviewingLife = false;
            RefreshAll();
        }

        void ReviewLife()
        {
            _reviewingLife = true;
            RefreshAll();
            ScrollLogToTop();
        }

        void ReturnToEnding()
        {
            _reviewingLife = false;
            RefreshAll();
        }

        void ScrollLogToTop()
        {
            if (_logScroll == null)
                return;

            Canvas.ForceUpdateCanvases();
            if (_logText != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_logText.rectTransform);
            if (_logScroll.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_logScroll.content);
            _logScroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
        }

        void OnPlayPrimaryClicked()
        {
            if (_session != null && _session.Phase == GamePhase.Ended)
            {
                ReturnToEnding();
                return;
            }

            _session.Advance();
        }

        void StartLife()
        {
            _logBuilder.Length = 0;
            if (_logText != null)
                _logText.text = string.Empty;
            _session.StartLife();
        }

        void Restart()
        {
            _reviewingLife = false;
            _logBuilder.Length = 0;
            if (_logText != null)
                _logText.text = string.Empty;
            _session.ResetToAllocate();
            RefreshAll();
        }

        #region UI Build

        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            EnsureEventSystem();

            // CreatePanel/Stretch accept GameObject or Transform to avoid CS1503 mismatches.
            var root = CreatePanel(canvasGo, "Root", new Color(0.1f, 0.11f, 0.13f, 1f));
            Stretch(root);

            _allocatePanel = CreatePanel(root, "AllocatePanel", new Color(0.12f, 0.13f, 0.16f, 1f));
            Stretch(_allocatePanel);
            BuildAllocatePanel(_allocatePanel.transform);

            _playPanel = CreatePanel(root, "PlayPanel", new Color(0.1f, 0.11f, 0.13f, 1f));
            Stretch(_playPanel);
            BuildPlayPanel(_playPanel.transform);

            _choicePanel = CreatePanel(root, "ChoicePanel", new Color(0f, 0f, 0f, 0.75f));
            Stretch(_choicePanel);
            BuildChoicePanel(_choicePanel.transform);

            _endPanel = CreatePanel(root, "EndPanel", new Color(0f, 0f, 0f, 0.82f));
            Stretch(_endPanel);
            BuildEndPanel(_endPanel.transform);

            _allocatePanel.SetActive(true);
            _playPanel.SetActive(false);
            _choicePanel.SetActive(false);
            _endPanel.SetActive(false);
        }

        void BuildAllocatePanel(Transform parent)
        {
            CreateText(parent, "Title", "人生模拟器", 56, TextAnchor.UpperCenter,
                new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f));

            CreateText(parent, "Hint", "天赋已按总和随机生成（每项 1~10）", 34, TextAnchor.UpperCenter,
                new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.82f));

            _pointsText = CreateText(parent, "Points", "随机天赋", 36, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.58f), new Vector2(0.9f, 0.72f));
            _pointsText.horizontalOverflow = HorizontalWrapMode.Wrap;

            float y = 0.52f;
            y = AddAllocRow(parent, "力量", "str", y);
            y = AddAllocRow(parent, "智力", "int", y);
            y = AddAllocRow(parent, "运气", "luck", y);
            AddAllocRow(parent, "家境", "family", y);

            var rerollBtn = CreateButton(parent, "重新随机", () => _session.RollAttributes());
            SetRect(rerollBtn.GetComponent<RectTransform>(), new Vector2(0.12f, 0.14f), new Vector2(0.48f, 0.22f));

            var startBtn = CreateButton(parent, "开始人生", StartLife);
            SetRect(startBtn.GetComponent<RectTransform>(), new Vector2(0.52f, 0.14f), new Vector2(0.88f, 0.22f));
        }

        float AddAllocRow(Transform parent, string label, string key, float top)
        {
            float bottom = top - 0.08f;
            CreateText(parent, label + "Label", label, 36, TextAnchor.MiddleLeft,
                new Vector2(0.2f, bottom), new Vector2(0.5f, top));

            var value = CreateText(parent, label + "Value", "1", 36, TextAnchor.MiddleCenter,
                new Vector2(0.55f, bottom), new Vector2(0.8f, top));
            _allocValueTexts[key] = value;
            return bottom - 0.01f;
        }

        void BuildPlayPanel(Transform parent)
        {
            _statsText = CreateText(parent, "Stats", "年龄 0", 32, TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.98f));
            _statsText.verticalOverflow = VerticalWrapMode.Overflow;

            var logHost = CreatePanel(parent, "LogHost", new Color(0.16f, 0.17f, 0.2f, 1f));
            SetRect(logHost.GetComponent<RectTransform>(), new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.79f));

            // Prefer editor-authored prefab; fall back to runtime build.
            var prefab = Resources.Load<GameObject>("UI/EventLogScroll");
            GameObject scrollRoot;
            if (prefab != null)
            {
                scrollRoot = Object.Instantiate(prefab, logHost.transform, false);
                Stretch(scrollRoot);
                _logScroll = scrollRoot.GetComponent<ScrollRect>();
                if (_logScroll == null)
                    _logScroll = scrollRoot.GetComponentInChildren<ScrollRect>(true);
                if (_logScroll != null && _logScroll.content != null)
                    _logText = _logScroll.content.GetComponentInChildren<Text>(true);
            }
            else
            {
                scrollRoot = BuildEventLogScroll(logHost.transform);
            }

            if (_logText != null)
            {
                const int logSize = 32;
                _logText.font = ResolveUiFont(logSize);
                _logText.fontSize = logSize;
                _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _logText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            _nextYearButton = CreateButton(parent, "下一季", OnPlayPrimaryClicked);
            SetRect(_nextYearButton.GetComponent<RectTransform>(), new Vector2(0.52f, 0.08f), new Vector2(0.95f, 0.16f));
            _nextYearButtonLabel = _nextYearButton.GetComponentInChildren<Text>();

            _confessButton = CreateButton(parent, "告白", () => _session.BeginConfessSelect());
            SetRect(_confessButton.GetComponent<RectTransform>(), new Vector2(0.05f, 0.08f), new Vector2(0.48f, 0.16f));
        }

        GameObject BuildEventLogScroll(Transform parent)
        {
            // Correct ScrollRect hierarchy:
            // ScrollRect
            //   Viewport (Mask + Image)
            //     Content (top-anchored + ContentSizeFitter)
            //       LogText
            var scrollGo = new GameObject("EventLogScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(parent, false);
            Stretch(scrollGo);
            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0.14f, 0.15f, 0.18f, 1f);
            scrollBg.raycastTarget = true;

            _logScroll = scrollGo.GetComponent<ScrollRect>();
            _logScroll.horizontal = false;
            _logScroll.vertical = true;
            _logScroll.movementType = ScrollRect.MovementType.Clamped;
            _logScroll.scrollSensitivity = 24f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            Stretch(viewportGo);
            var viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = true;
            var mask = viewportGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter), typeof(VerticalLayoutGroup));
            content.transform.SetParent(viewportGo.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            _logText = CreateText(content.transform, "LogText", string.Empty, 32, TextAnchor.UpperLeft,
                Vector2.zero, Vector2.one);
            var logRt = _logText.rectTransform;
            logRt.anchorMin = new Vector2(0f, 1f);
            logRt.anchorMax = new Vector2(1f, 1f);
            logRt.pivot = new Vector2(0.5f, 1f);
            logRt.sizeDelta = new Vector2(0f, 0f);
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Overflow;
            var textFitter = _logText.gameObject.AddComponent<ContentSizeFitter>();
            textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var le = _logText.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.flexibleWidth = 1f;

            _logScroll.viewport = viewportGo.GetComponent<RectTransform>();
            _logScroll.content = contentRt;
            return scrollGo;
        }

        void BuildChoicePanel(Transform parent)
        {
            var box = CreatePanel(parent, "ChoiceBox", new Color(0.18f, 0.19f, 0.23f, 1f));
            SetRect(box.GetComponent<RectTransform>(), new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.75f));

            _choicePrompt = CreateText(box.transform, "Prompt", "选择", 34, TextAnchor.UpperCenter,
                new Vector2(0.08f, 0.7f), new Vector2(0.92f, 0.95f));
            _choicePrompt.horizontalOverflow = HorizontalWrapMode.Wrap;

            var scrollGo = new GameObject("ChoiceScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(box.transform, false);
            SetRect(scrollGo.GetComponent<RectTransform>(), new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.65f));
            scrollGo.GetComponent<Image>().color = new Color(0.16f, 0.17f, 0.2f, 1f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            Stretch(viewportGo);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var rootGo = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rootGo.transform.SetParent(viewportGo.transform, false);
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(1f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            var layout = rootGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            var fitter = rootGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.content = rootRt;
            _choiceButtonRoot = rootGo.transform;
        }

        void BuildEndPanel(Transform parent)
        {
            CreateText(parent, "EndTitle", "人生结算", 52, TextAnchor.UpperCenter,
                new Vector2(0.15f, 0.7f), new Vector2(0.85f, 0.85f));

            _summaryText = CreateText(parent, "Summary", string.Empty, 32, TextAnchor.UpperCenter,
                new Vector2(0.1f, 0.34f), new Vector2(0.9f, 0.7f));
            _summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _summaryText.verticalOverflow = VerticalWrapMode.Overflow;

            var review = CreateButton(parent, "回顾一生", ReviewLife);
            SetRect(review.GetComponent<RectTransform>(), new Vector2(0.12f, 0.2f), new Vector2(0.48f, 0.3f));

            var restart = CreateButton(parent, "再来一世", Restart);
            SetRect(restart.GetComponent<RectTransform>(), new Vector2(0.52f, 0.2f), new Vector2(0.88f, 0.3f));
        }

        static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static GameObject CreatePanel(GameObject parent, string name, Color color)
        {
            return CreatePanel(parent.transform, name, color);
        }

        static Font _uiFont;

        static Font ResolveUiFont(int size)
        {
            if (_uiFont != null)
                return _uiFont;

            // Builtin Arial/LegacyRuntime cannot render Chinese; prefer OS CJK fonts.
            string[] candidates =
            {
                "Microsoft YaHei UI",
                "Microsoft YaHei",
                "SimHei",
                "PingFang SC",
                "Noto Sans CJK SC",
                "Arial"
            };

            _uiFont = Font.CreateDynamicFontFromOSFont(candidates, size);
            if (_uiFont == null)
                _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _uiFont;
        }

        static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = ResolveUiFont(size);
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            SetRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            return text;
        }

        Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.7f, 1f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = CreateText(go.transform, "Label", label, 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            Stretch(text.rectTransform);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 56f;
            le.preferredHeight = 56f;
            le.flexibleWidth = 1f;
            return button;
        }

        static void Stretch(Component c)
        {
            SetRect(c.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        }

        static void Stretch(GameObject go)
        {
            Stretch(go.transform);
        }

        static void SetRect(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
