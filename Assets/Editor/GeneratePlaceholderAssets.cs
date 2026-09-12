using System.Collections.Generic;
using System.IO;
using Data.Player;
using Spine.Unity;
using Tools;
using UI;
using UI.Sub;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Arknights.EditorTools {
    /// <summary>
    /// 生成占位资源 / Generates placeholder bootstrap assets.
    ///
    /// The real Arknights art, audio and data are not in this repository, so every Asset.Load
    /// returns null and nothing renders. This builds the minimum set of prefabs and data the
    /// boot path needs, using only Unity's built-in UI sprites, so the project can be run and
    /// exercised end to end.
    ///
    /// Everything lands under Resources/. Asset.Load checks AssetBundles *first*, so once real
    /// bundles are supplied they take over automatically and these placeholders are ignored -
    /// no cleanup required.
    ///
    /// Prefabs are built programmatically rather than authored as .prefab YAML because the
    /// serialised field wiring (Buttons, InputFields, CanvasGroups, Injections) is far easier to
    /// get right - and to re-run - in code.
    /// </summary>
    public static class GeneratePlaceholderAssets {

        private const string ResourcesRoot = "Assets/Arknights/Resources";
        private const string UIPrefabDir = ResourcesRoot + "/Prefab/UI";
        private const string GamePrefabDir = ResourcesRoot + "/Prefab/Game";
        private const string UserDataDir = ResourcesRoot + "/Data/User";
        private const string FontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        // 明日方舟风格配色: 近黑底 + 石板灰面板 + 橙色强调
        // Arknights-flavoured palette. There is still no real art here, so the look comes from
        // colour, procedurally drawn chamfered "cut corner" panels, and layout - nothing else.
        // 面板必须完全不透明: 工程是线性色彩空间, 面板底下就是橙色边框, 哪怕3%的透明度
        // 在线性空间里也会把整块面板染成酱红色
        // The panel fills must be fully opaque. The project renders in Linear colour space and
        // these sit directly on the orange frame, where even 3% alpha blends enough of a bright
        // orange through to turn the whole panel maroon.
        private static readonly Color Ink = new Color(0.043f, 0.047f, 0.055f);
        private static readonly Color Panel = new Color(0.106f, 0.118f, 0.141f, 1f);
        private static readonly Color Accent = new Color(1.00f, 0.42f, 0.09f);
        private static readonly Color AccentDim = new Color(0.60f, 0.27f, 0.10f);
        private static readonly Color Field = new Color(0.060f, 0.065f, 0.075f);
        private static readonly Color TextDim = new Color(0.55f, 0.57f, 0.61f);
        private static readonly Color Hostile = new Color(0.82f, 0.20f, 0.16f);
        private static readonly Color Ranged = new Color(0.25f, 0.62f, 1.00f);

        // 登录界面照着参考图走: 上下黑条 + 首页浅灰底 + 登录/注册纯黑底 + 灰色方块按钮, 没有橙色切角
        // The login screen follows the reference shots instead of the chamfered orange styling used
        // elsewhere: black top/bottom bars, a pale grey home backdrop that a near-opaque overlay
        // blacks out for the login and register steps, and flat grey rectangles for every control.
        private static readonly Color BarInk = new Color(0.075f, 0.075f, 0.080f);
        private static readonly Color Daylight = new Color(0.855f, 0.855f, 0.862f);
        private static readonly Color ButtonFace = new Color(0.322f, 0.325f, 0.333f, 0.95f);
        private static readonly Color FieldFace = new Color(0.400f, 0.404f, 0.412f, 0.55f);
        private static readonly Color EdgeLight = new Color(1f, 1f, 1f, 0.30f);
        private static readonly Color LinkBlue = new Color(0.00f, 0.69f, 1.00f);
        private static readonly Color LoadingLine = new Color(0.910f, 0.784f, 0.118f);
        private static readonly Color WireLine = new Color(0.78f, 0.14f, 0.18f);
        private static readonly Color WireNode = new Color(1.00f, 0.42f, 0.40f);

        // 主界面: 半透明白色斜切磁贴 + 深色字, 采购中心那一组是蓝底白字
        // Home screen: translucent white skewed tiles with dark type, except the store cluster,
        // which is the one blue-on-white group in the reference.
        private static readonly Color TileLight = new Color(0.93f, 0.94f, 0.95f, 0.86f);
        private static readonly Color TileBlue = new Color(0.16f, 0.62f, 0.85f, 0.95f);
        private static readonly Color TileInk = new Color(0.08f, 0.09f, 0.10f);
        private static readonly Color HomeInk = new Color(0.16f, 0.17f, 0.19f);

        private const float TileSkew = 14f;

        private const float ChamferBigCorner = 28f;
        private const float ChamferSmallCorner = 13f;
        private const float FrameGap = 5f;

        private static Font font;
        private static Sprite chamferBig;
        private static Sprite chamferSmall;
        private static Sprite wireSphere;
        private static Sprite skewTile;
        private static Sprite levelRing;

        [MenuItem("Arknights/Placeholders/Generate Bootstrap Assets", false, 0)]
        public static void Generate() {
            font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            EnsureFolder(UIPrefabDir);
            EnsureFolder(GamePrefabDir);
            EnsureFolder(UserDataDir);

            chamferBig = BuildChamferSprite("T_ChamferBig", 160, (int)ChamferBigCorner);
            chamferSmall = BuildChamferSprite("T_ChamferSmall", 80, (int)ChamferSmallCorner);
            wireSphere = BuildWireSphereSprite("T_WireSphere", 512);
            skewTile = BuildSkewSprite("T_SkewTile", 128, (int)TileSkew);
            levelRing = BuildRingSprite("T_LevelRing", 256, 0.80f);

            BuildCameraPrefab();
            BuildLoginUIPrefab();
            BuildCommonDialogPrefab();
            BuildHomeUIPrefab();
            BuildCharPrefab();
            BuildMonsterPrefab();
            BuildCharPlacePrefab();
            BuildAtkRangeDisplayPrefab();
            BuildPlayerData("Saukiya", "123456");
            BuildPlayerData("Test", "123456");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Placeholders] Generated bootstrap assets into " + ResourcesRoot +
                      ". Press Play on Assets/Arknights/Scenes/StartMenu.unity. " +
                      "Log in with Saukiya / 123456.");
        }

        [MenuItem("Arknights/Placeholders/Delete Generated Assets", false, 1)]
        public static void Delete() {
            foreach (string dir in new[] { UIPrefabDir, GamePrefabDir, ResourcesRoot + "/Data" }) {
                if (AssetDatabase.IsValidFolder(dir)) AssetDatabase.DeleteAsset(dir);
            }
            AssetDatabase.Refresh();
            Debug.Log("[Placeholders] Removed generated placeholder assets.");
        }

        // ------------------------------------------------------------------ Camera + Canvas

        /// <summary>
        /// UIManager.Initialization 需要: 根节点 + 名为"Canvas"的直接子节点 + CanvasScaler
        /// UIManager.Initialization requires a root, a DIRECT child named exactly "Canvas",
        /// and a CanvasScaler on it whose referenceResolution it reads.
        /// </summary>
        private static void BuildCameraPrefab() {
            GameObject root = new GameObject("Camera");
            Camera cam = root.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ink;
            cam.orthographic = true;

            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root.transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // UIManager 以宽适配 / project adapts on width
            canvasGo.AddComponent<GraphicRaycaster>();

            // 没有EventSystem按钮和输入框点不动 / Without an EventSystem nothing is clickable.
            GameObject events = new GameObject("EventSystem");
            events.transform.SetParent(root.transform, false);
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();

            SavePrefab(root, "Camera");
        }

        // ------------------------------------------------------------------ LoginUI

        private static void BuildLoginUIPrefab() {
            GameObject root = NewUIRoot("LoginUI");
            LoginUI ui = root.AddComponent<LoginUI>();

            // 首页是浅灰底, 登录/注册时 backGround 这层黑幕淡入把它盖掉
            // The home step is pale grey; backGround is the blackout LoginUI fades in over it on
            // the way to login/register, and back out on the way home.
            // 必须完全不透明: 线性空间里就算96%也会透出底下的浅灰, 实测变成0.19的灰而不是黑
            // It has to be fully opaque. The project renders in Linear space, where even 4% of the
            // pale backdrop showing through lands at ~0.19 grey instead of black - measured, not
            // guessed. The CanvasGroup is what fades it in, so opacity here costs nothing.
            Rect(root, "BaseBackground", Stretch).AddComponent<Image>().color = Daylight;
            ui.backGround = Group(root, "BackGround", Stretch, new Color(0.043f, 0.043f, 0.047f, 1f), 0f);

            // sphere 被 DOAnchorPosY / DOScale 驱动 / driven by DOAnchorPosY and DOScale
            // 必须排在黑幕之后: 参考图里浅底和黑底两种情况下球都看得见
            // Ordered after the blackout on purpose - the ball stays visible on both the pale and
            // the black backdrop in the reference shots.
            GameObject sphereGo = Rect(root, "Sphere", r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(620, 620);
                r.anchoredPosition = new Vector2(0, 263);
            });
            Image sphereImg = sphereGo.AddComponent<Image>();
            sphereImg.color = Color.white;
            sphereImg.sprite = wireSphere;
            sphereImg.raycastTarget = false;
            ui.sphere = (RectTransform)sphereGo.transform;

            // sphereCamera.DOColor 动画的是背景色 / DOColor animates the background colour
            GameObject sphereCamGo = new GameObject("SphereCamera");
            sphereCamGo.transform.SetParent(root.transform, false);
            Camera sphereCam = sphereCamGo.AddComponent<Camera>();
            sphereCam.clearFlags = CameraClearFlags.SolidColor;
            sphereCam.backgroundColor = new Color(0.74f, 0.36f, 0.36f, 0f);
            sphereCam.cullingMask = 0;
            // 关掉渲染, 否则它的 SolidColor 会把整个屏幕刷掉; DOColor 只改背景色, 不需要它真的渲染
            // Disabled on purpose: a second SolidColor camera would clear the whole screen.
            // LoginUI only tweens its backgroundColor, which works fine on a disabled camera.
            sphereCam.enabled = false;
            ui.sphereCamera = sphereCam;

            // ---- home: 标题 + 两个并排按钮, 深色字压在浅灰底上
            // Home sits on the pale backdrop, so its type is dark - the other two steps run black.
            ui.homePanel = PanelLayer(root, "HomePanel", 1f);
            GameObject home = ui.homePanel.gameObject;

            Text wordmark = Label(home, "Wordmark", "ARKNIGHTS", 148, new Color(0.06f, 0.06f, 0.07f),
                new Vector2(0, 110));
            ((RectTransform)wordmark.transform).sizeDelta = new Vector2(1400, 190);

            GameObject ribbon = Rect(home, "Ribbon", r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(330, 34);
                r.anchoredPosition = Vector2.zero;
            });
            ribbon.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.11f);
            Text ribbonText = Label(ribbon, "RibbonText", "R H O D E S   I S L A N D", 18,
                new Color(0.88f, 0.88f, 0.90f), Vector2.zero);
            ((RectTransform)ribbonText.transform).sizeDelta = new Vector2(330, 34);

            // 宽度是按"REGISTER ACCOUNT"这条最长的标题定的: Style() 把Text设成Overflow,
            // 装不下不会换行而是直接溢出到按钮外面
            // Sized for "REGISTER ACCOUNT", the longest caption: Style() leaves Text on Overflow,
            // so a caption that does not fit spills outside the button rather than wrapping.
            ui.home_login = FlatBtn(home, "home_login", "ACCOUNT LOGIN",
                new Vector2(-152, -160), new Vector2(290, 74), 24, false);
            ui.home_register = FlatBtn(home, "home_register", "REGISTER ACCOUNT",
                new Vector2(152, -160), new Vector2(290, 74), 24, true);

            // ---- login: 返回键在左上, 两个输入框和登录键同宽居中
            // Login: back arrow top-left, then two fields and a button all sharing one width.
            ui.loginPanel = PanelLayer(root, "LoginPanel", 0f);
            GameObject login = ui.loginPanel.gameObject;
            login.SetActive(false);

            ui.login_back = BackBtn(login, "login_back");
            ui.login_userName = FlatInput(login, "login_userName", "Account", new Vector2(0, -60));
            ui.login_password = FlatInput(login, "login_password", "Password", new Vector2(0, -135));
            ui.login_password.contentType = InputField.ContentType.Password;
            ui.login_enter = FlatBtn(login, "login_enter", "LOGIN",
                new Vector2(0, -262), new Vector2(FieldWidth, 64), 26, false);

            // ---- register
            ui.registerPanel = PanelLayer(root, "RegisterPanel", 0f);
            GameObject reg = ui.registerPanel.gameObject;
            reg.SetActive(false);

            ui.register_back = BackBtn(reg, "register_back");
            ui.register_userName = FlatInput(reg, "register_userName", "Username", new Vector2(0, 18));
            ui.register_password = FlatInput(reg, "register_password", "Enter password", new Vector2(0, -57));
            ui.register_password.contentType = InputField.ContentType.Password;
            ui.register_rePassword = FlatInput(reg, "register_rePassword", "Enter password again",
                new Vector2(0, -132));
            ui.register_rePassword.contentType = InputField.ContentType.Password;
            ui.register_toggle = FlatCheck(reg, "register_toggle",
                "I have read and agree to the <color=#00B0FF>Registration Agreement</color> " +
                "and <color=#00B0FF>Privacy Agreement</color>", new Vector2(0, -196));
            ui.register_enter = FlatBtn(reg, "register_enter", "REGISTER",
                new Vector2(0, -282), new Vector2(FieldWidth, 64), 26, false);

            // ---- loading: 两根进度条从左右往中间合成一条黄线 / the two bars close into one yellow line
            ui.loadingPanel = PanelLayer(root, "LoadingPanel", 0f);
            GameObject loading = ui.loadingPanel.gameObject;
            loading.SetActive(false);
            ServerPlate(loading);
            ui.loading_bar1 = Bar(loading, "loading_bar1", true);
            ui.loading_bar2 = Bar(loading, "loading_bar2", false);
            Label(loading, "Status", "Attempting to establish a neural link with the Pigeon Server",
                20, new Color(0.72f, 0.72f, 0.75f), new Vector2(0, -330));

            // ---- 上下黑条压在所有面板之上 / the bars overlay every panel, as in the reference
            GameObject topBar = Rect(root, "TopBar", r => {
                r.anchorMin = new Vector2(0, 1);
                r.anchorMax = new Vector2(1, 1);
                r.pivot = new Vector2(0.5f, 1);
                r.sizeDelta = new Vector2(0, 86);
            });
            topBar.AddComponent<Image>().color = BarInk;

            GameObject bottomBar = Rect(root, "BottomBar", r => {
                r.anchorMin = new Vector2(0, 0);
                r.anchorMax = new Vector2(1, 0);
                r.pivot = new Vector2(0.5f, 0);
                r.sizeDelta = new Vector2(0, 96);
            });
            bottomBar.AddComponent<Image>().color = BarInk;

            // 参考图这里是发行商logo, 本仓库没有美术资源, 放一行说明代替
            // The reference carries publisher logos here; with no art in the repo this says what
            // the build actually is instead of faking someone's branding.
            Text mark = Label(bottomBar, "Mark", "PLACEHOLDER BUILD   -   no art assets, see SETUP.md",
                18, new Color(0.62f, 0.62f, 0.65f), Vector2.zero);
            RectTransform markRect = (RectTransform)mark.transform;
            markRect.anchorMin = markRect.anchorMax = new Vector2(0, 0.5f);
            markRect.pivot = new Vector2(0, 0.5f);
            markRect.sizeDelta = new Vector2(700, 40);
            markRect.anchoredPosition = new Vector2(40, 0);
            mark.alignment = TextAnchor.MiddleLeft;

            ui.lowerRightPanel = PanelLayer(root, "LowerRightPanel", 1f);
            GameObject lower = ui.lowerRightPanel.gameObject;
            Plaque(lower, "Announcement", "No Announcements", -488);
            Plaque(lower, "Agreement", "User Agreement", -300);

            Text credit = Label(lower, "Credit", "REMAKE\nby Saukiya", 20,
                new Color(0.92f, 0.92f, 0.94f), Vector2.zero);
            RectTransform creditRect = (RectTransform)credit.transform;
            creditRect.anchorMin = creditRect.anchorMax = new Vector2(1, 0);
            creditRect.pivot = new Vector2(1, 0);
            creditRect.sizeDelta = new Vector2(180, 70);
            creditRect.anchoredPosition = new Vector2(-32, 14);

            SavePrefab(root, "LoginUI");
        }

        private const float FieldWidth = 540f;

        /// <summary>
        /// LoginUI.cs 对进度条调用 GetComponentInChildren&lt;Text&gt;(), 所以必须有子Text
        /// LoginUI calls GetComponentInChildren&lt;Text&gt;() on each bar, so a child Text is required.
        ///
        /// 两根条共用一个Y: 一根从左边长过来, 一根从右边长过来, 合起来就是参考图那条黄线
        /// Both bars share one Y. bar1 grows rightward from the left edge and bar2 leftward from
        /// the right, so when the tween brings both to 0.5 they meet in the middle and read as the
        /// single yellow rule the reference draws across the loading screen. The tween drives the
        /// fill's own anchors, so the track behind it has to span the full width.
        /// </summary>
        private static RectTransform Bar(GameObject parent, string name, bool leftToRight) {
            GameObject track = Rect(parent, name + "_track", r => {
                r.anchorMin = new Vector2(0f, 0.5f);
                r.anchorMax = new Vector2(1f, 0.5f);
                r.sizeDelta = new Vector2(0, 5);
                r.anchoredPosition = new Vector2(0, -30);
            });
            track.AddComponent<Image>().color =
                new Color(LoadingLine.r, LoadingLine.g, LoadingLine.b, 0.07f);

            GameObject fill = new GameObject(name, typeof(RectTransform));
            fill.transform.SetParent(track.transform, false);
            RectTransform fr = (RectTransform)fill.transform;
            fr.anchorMin = leftToRight ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            fr.anchorMax = leftToRight ? new Vector2(0.02f, 1f) : new Vector2(1f, 1f);
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = LoadingLine;

            // 百分比跟着增长的那一头, 抬到线上方, 两根条会合时才不会叠在一起
            // The readout rides the growing tip and sits above the rule, so the two do not overlap
            // when both bars arrive at the centre together.
            GameObject label = Rect(fill, name + "_text", r => {
                r.anchorMin = r.anchorMax = new Vector2(leftToRight ? 1f : 0f, 0.5f);
                r.pivot = new Vector2(leftToRight ? 1f : 0f, 0.5f);
                r.sizeDelta = new Vector2(150, 26);
                r.anchoredPosition = new Vector2(leftToRight ? -10 : 10, 24);
            });
            Text t = label.AddComponent<Text>();
            Style(t, 20, LoadingLine);
            t.alignment = leftToRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            t.text = "0%";

            return fr;
        }

        /// <summary>
        /// 参考图里没有面板底框, 控件直接浮在背景上, 所以一"页"只有RectTransform+CanvasGroup
        /// The reference screens have no panel box - the controls sit straight on the backdrop - so
        /// a step is just a full-screen RectTransform plus the CanvasGroup LoginUI fades.
        /// </summary>
        private static CanvasGroup PanelLayer(GameObject parent, string name, float alpha) {
            CanvasGroup group = Rect(parent, name, Stretch).AddComponent<CanvasGroup>();
            group.alpha = alpha;
            return group;
        }

        /// <summary>
        /// 参考图的按钮就是一个灰方块加一圈浅边 / a flat grey rectangle with a light edge.
        /// marker 画注册键左边那个小三角 / marker draws the triangle the register button carries.
        /// </summary>
        private static Button FlatBtn(GameObject parent, string name, string caption, Vector2 pos,
                                      Vector2 size, int fontSize, bool marker) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = size;
                r.anchoredPosition = pos;
            });
            Image img = go.AddComponent<Image>();
            img.color = ButtonFace;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = img;
            Border(go, EdgeLight, 2f);

            // 有小三角时标题要往右让一截, 否则长标题会压在三角上
            // With a marker the caption has to give up the left inset, or a long caption runs
            // straight over the triangle.
            Text t = Label(go, "Text", caption, fontSize, Color.white, Vector2.zero);
            RectTransform captionRect = (RectTransform)t.transform;
            captionRect.sizeDelta = marker ? new Vector2(size.x - 30, size.y) : size;
            captionRect.anchoredPosition = marker ? new Vector2(15, 0) : Vector2.zero;

            if (marker) {
                Text triangle = Label(go, "Marker", "▸", fontSize, Color.white, Vector2.zero);
                RectTransform triangleRect = (RectTransform)triangle.transform;
                triangleRect.anchorMin = triangleRect.anchorMax = new Vector2(0, 0.5f);
                triangleRect.pivot = new Vector2(0, 0.5f);
                triangleRect.sizeDelta = new Vector2(30, size.y);
                triangleRect.anchoredPosition = new Vector2(14, 0);
            }
            return button;
        }

        /// <summary>左上角的返回键 / the back chevron pinned to the top-left.</summary>
        private static Button BackBtn(GameObject parent, string name) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0, 1);
                r.sizeDelta = new Vector2(152, 66);
                r.anchoredPosition = new Vector2(48, -128);
            });
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.30f, 0.30f, 0.31f, 0.75f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = img;

            Text t = Label(go, "Text", "‹", 52, Color.white, new Vector2(0, 4));
            ((RectTransform)t.transform).sizeDelta = new Vector2(152, 66);
            return button;
        }

        private static InputField FlatInput(GameObject parent, string name, string placeholder, Vector2 pos) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(FieldWidth, 64);
                r.anchoredPosition = pos;
            });
            Image img = go.AddComponent<Image>();
            img.color = FieldFace;

            Text text = FieldText(go, "Text", 24, Color.white);
            Text ph = FieldText(go, "Placeholder", 24, new Color(0.82f, 0.82f, 0.85f, 0.65f));
            ph.fontStyle = FontStyle.Italic;
            ph.text = placeholder;

            InputField field = go.AddComponent<InputField>();
            field.targetGraphic = img;
            field.textComponent = text;
            field.placeholder = ph;
            return field;
        }

        private static Text FieldText(GameObject parent, string name, int size, Color color) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(22, 6);
                r.offsetMax = new Vector2(-22, -6);
            });
            Text t = go.AddComponent<Text>();
            Style(t, size, color);
            t.alignment = TextAnchor.MiddleLeft;
            return t;
        }

        private static Toggle FlatCheck(GameObject parent, string name, string caption, Vector2 pos) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(FieldWidth, 40);
                r.anchoredPosition = pos;
            });
            GameObject boxGo = Rect(go, "Background", r => {
                r.anchorMin = r.anchorMax = new Vector2(0, 0.5f);
                r.pivot = new Vector2(0, 0.5f);
                r.sizeDelta = new Vector2(26, 26);
                r.anchoredPosition = Vector2.zero;
            });
            Image box = boxGo.AddComponent<Image>();
            box.color = new Color(0.62f, 0.63f, 0.65f, 0.85f);

            GameObject markGo = Rect(boxGo, "Checkmark", r => {
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(5, 5);
                r.offsetMax = new Vector2(-5, -5);
            });
            Image mark = markGo.AddComponent<Image>();
            mark.color = new Color(0.10f, 0.10f, 0.12f);

            Text t = Label(go, "Label", caption, 19, new Color(0.88f, 0.88f, 0.90f), Vector2.zero);
            RectTransform textRect = (RectTransform)t.transform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0, 0.5f);
            textRect.pivot = new Vector2(0, 0.5f);
            textRect.sizeDelta = new Vector2(FieldWidth - 40, 40);
            textRect.anchoredPosition = new Vector2(38, 0);
            t.alignment = TextAnchor.MiddleLeft;

            Toggle toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = mark;
            toggle.isOn = false;
            return toggle;
        }

        /// <summary>加载页中间那块服务器信息板 / the server plate shown mid-load.</summary>
        private static void ServerPlate(GameObject parent) {
            GameObject plate = Rect(parent, "ServerPlate", r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(196, 330);
                r.anchoredPosition = new Vector2(0, 60);
            });
            plate.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.20f, 0.55f);
            Border(plate, new Color(1f, 1f, 1f, 0.45f), 2f);

            Label(plate, "ServerCaption", "SERVER", 17, new Color(0.72f, 0.72f, 0.75f), new Vector2(0, 116));
            Label(plate, "ServerName", "TERRA", 34, Color.white, new Vector2(0, 80));
            Label(plate, "ServerIndex", "#0", 86, Color.white, new Vector2(0, -6));
            Label(plate, "ServerLoad", "100%", 20, LoadingLine, new Vector2(0, -122));
        }

        /// <summary>
        /// 底栏右下角那两块牌子 / the two plates in the bottom-right of every reference shot.
        /// LoginUI 没有对应字段, 所以只做外观不做交互 - 按下去没反应的按钮比不做按钮更糟
        /// LoginUI has no fields for these, so they are styled labels rather than Buttons: a button
        /// that swallows the click and does nothing is worse than something that never looked
        /// clickable. Wire them up here if the screens behind them ever exist.
        /// </summary>
        private static void Plaque(GameObject parent, string name, string caption, float x) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(1, 0);
                r.pivot = new Vector2(1, 0);
                r.sizeDelta = new Vector2(174, 52);
                r.anchoredPosition = new Vector2(x, 22);
            });
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.24f, 0.24f, 0.25f, 0.95f);
            img.raycastTarget = false;
            Border(go, new Color(1f, 1f, 1f, 0.22f), 1.5f);

            Text t = Label(go, "Text", caption, 18, new Color(0.90f, 0.90f, 0.92f), Vector2.zero);
            ((RectTransform)t.transform).sizeDelta = new Vector2(174, 52);
        }

        /// <summary>
        /// uGUI 没有描边属性, 只能用四条细Image拼一圈
        /// uGUI has no border property, so an outline is four thin stretched Images. They are added
        /// before the caption so the text always draws over them, and none of them take raycasts.
        /// </summary>
        private static void Border(GameObject parent, Color color, float thickness) {
            Edge(parent, "EdgeTop", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, thickness), new Vector2(0, -thickness * 0.5f), color);
            Edge(parent, "EdgeBottom", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, thickness), new Vector2(0, thickness * 0.5f), color);
            Edge(parent, "EdgeLeft", new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(thickness, 0), new Vector2(thickness * 0.5f, 0), color);
            Edge(parent, "EdgeRight", new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(thickness, 0), new Vector2(-thickness * 0.5f, 0), color);
        }

        private static void Edge(GameObject parent, string name, Vector2 min, Vector2 max,
                                 Vector2 size, Vector2 offset, Color color) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = min;
                r.anchorMax = max;
                r.sizeDelta = size;
                r.anchoredPosition = offset;
            });
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        // ------------------------------------------------------------------ CommonDialogUI

        private static void BuildCommonDialogPrefab() {
            GameObject root = NewUIRoot("CommonDialogUI");
            CommonDialogUI ui = root.AddComponent<CommonDialogUI>();

            // Show() 会对 blur.material 设置 _Size, 给它一份独立材质避免污染共享材质
            // Show() sets _Size on blur.material; give it its own material so the shared
            // default UI material is never mutated. The property does not exist on UI/Default,
            // which Unity treats as a harmless no-op.
            GameObject blurGo = Rect(root, "blur", Stretch);
            Image blur = blurGo.AddComponent<Image>();
            blur.color = new Color(0, 0, 0, 0.55f);
            blur.material = PlaceholderMaterial();
            ui.blur = blur;

            // 参考图的弹窗是一条横贯屏幕的色带, 不是居中的方框
            // The reference dialog is a band running the full width of the screen rather than a
            // centred box, so Box stretches horizontally and only its height is fixed.
            GameObject box = Rect(root, "Box", r => {
                r.anchorMin = new Vector2(0, 0.5f);
                r.anchorMax = new Vector2(1, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(0, 280);
                r.anchoredPosition = new Vector2(0, 40);
            });

            ui.blackGround = Rect(box, "blackGround", Stretch);
            ui.blackGround.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.17f, 0.94f);
            ui.whiteGround = Rect(box, "whiteGround", Stretch);
            ui.whiteGround.AddComponent<Image>().color = new Color(0.90f, 0.90f, 0.92f, 0.96f);
            ui.blackGround.SetActive(false);
            ui.whiteGround.SetActive(false);

            // 字色由 CommonDialogUI.GetText() 在运行时按黑底/白底切换, 这里只定排版
            // Colour is chosen at runtime by CommonDialogUI.GetText() from the ground type, so this
            // only fixes the typography.
            GameObject msg = Rect(box, "message", r => {
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(80, 0);
                r.offsetMax = new Vector2(-80, 0);
            });
            Text msgText = msg.AddComponent<Text>();
            Style(msgText, 30, Color.white);
            msgText.supportRichText = true;
            ui.message = msgText;

            GameObject one = Rect(root, "mode_One", Stretch);
            ui.mode_One = one;
            ui.mode_One_back = RoundBtn(one, "mode_One_back", "✓", new Vector2(0, -200));

            GameObject two = Rect(root, "mode_Two", Stretch);
            ui.mode_Two = two;
            ui.mode_Two_enter = RoundBtn(two, "mode_Two_enter", "✓", new Vector2(-90, -200));
            ui.mode_Two_back = RoundBtn(two, "mode_Two_back", "✕", new Vector2(90, -200));
            two.SetActive(false);

            SavePrefab(root, "CommonDialogUI");
        }

        /// <summary>参考图里确认键是个白色圆钮 / the reference confirm control is a white disc.</summary>
        private static Button RoundBtn(GameObject parent, string name, string glyph, Vector2 pos) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(74, 74);
                r.anchoredPosition = pos;
            });
            Image img = go.AddComponent<Image>();
            img.sprite = Builtin("Knob");
            img.color = Color.white;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = img;

            Text t = Label(go, "Glyph", glyph, 34, new Color(0.10f, 0.10f, 0.12f), Vector2.zero);
            ((RectTransform)t.transform).sizeDelta = new Vector2(74, 74);
            return button;
        }

        // ------------------------------------------------------------------ HomeUI (Lua driven)

        /// <summary>
        /// HomeUI 由 Lua 驱动, injections 的名字必须和 HomeUI.lua.txt 里的全局变量对应
        /// HomeUI is Lua driven: the injection names must match the globals HomeUI.lua.txt reads.
        /// </summary>
        private static void BuildHomeUIPrefab() {
            GameObject root = NewUIRoot("HomeUI");
            RuaUI ui = root.AddComponent<RuaUI>();

            Rect(root, "BackGround", Stretch).AddComponent<Image>().color = HomeInk;

            // move1 / move2 由 Lua 跟着鼠标做视差, 所以它们就是背景层
            // Lua parallaxes these two against the mouse, so they ARE the backdrop. The centre of
            // the screen is deliberately left clear - that is where the operator art would go.
            GameObject move1 = Rect(root, "move1", Centered(2400, 1400));
            move1.AddComponent<Image>().color = new Color(0.22f, 0.24f, 0.27f, 0.55f);
            Beam(move1, "Beam1", new Vector2(-160, 250), new Vector2(2100, 90), 0.16f);
            Beam(move1, "Beam2", new Vector2(120, -80), new Vector2(2100, 140), 0.12f);
            Beam(move1, "Beam3", new Vector2(-40, -360), new Vector2(2100, 70), 0.10f);

            GameObject move2 = Rect(root, "move2", Centered(1500, 900));
            move2.AddComponent<Image>().color = new Color(0.30f, 0.33f, 0.37f, 0.30f);
            Beam(move2, "Strut1", new Vector2(-420, 0), new Vector2(26, 900), 0.22f);
            Beam(move2, "Strut2", new Vector2(330, -60), new Vector2(26, 760), 0.18f);

            // ---- 左侧: 等级环 + 名字 + 罗德岛横幅 / level ring, name, Rhodes Island banner
            GameObject plate = Rect(root, "PlayerPlate", At(new Vector2(-742, 10), new Vector2(420, 300)));
            plate.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.08f, 0.55f);
            Text banner = Label(plate, "Banner", "RHODES ISLAND", 22, new Color(1, 1, 1, 0.28f),
                new Vector2(10, -108));
            ((RectTransform)banner.transform).sizeDelta = new Vector2(420, 40);

            GameObject ringTrack = Rect(root, "expTrack", At(new Vector2(-792, 75), new Vector2(190, 190)));
            Image ringTrackImage = ringTrack.AddComponent<Image>();
            ringTrackImage.sprite = levelRing;
            ringTrackImage.color = new Color(1, 1, 1, 0.12f);

            // 经验条做成环形: Lua 只设 fillAmount, 填充方式是在这里定的
            // The exp readout is a ring here. Lua only ever assigns fillAmount, so the fill method
            // is ours to choose - Radial360 from the top is what the reference draws.
            GameObject expGo = Rect(root, "expPercentage", At(new Vector2(-792, 75), new Vector2(190, 190)));
            Image exp = expGo.AddComponent<Image>();
            exp.sprite = levelRing;
            exp.color = new Color(0.98f, 0.78f, 0.16f);
            exp.type = Image.Type.Filled;           // fillAmount 只有 Filled 才有视觉效果
            exp.fillMethod = Image.FillMethod.Radial360;
            exp.fillOrigin = (int)Image.Origin360.Top;
            exp.fillClockwise = true;

            Text pLevel = Label(root, "playerLevel", "1", 58, Color.white, new Vector2(-792, 86));
            Label(root, "levelCaption", "LV", 20, new Color(1, 1, 1, 0.65f), new Vector2(-792, 44));
            Text pName = Label(root, "playerName", "Doctor", 34, Color.white, new Vector2(-770, -44));
            ((RectTransform)pName.transform).sizeDelta = new Vector2(360, 50);

            // ---- 左上角四个图标 / the four icons across the top-left
            IconButton(root, "SettingsIcon", IconGlyph.Gear, new Vector2(-866, 434), ui, "SettingUI");
            IconButton(root, "AlertIcon", IconGlyph.Alert, new Vector2(-770, 434), null, null);
            IconButton(root, "MailIcon", IconGlyph.Mail, new Vector2(-674, 434), null, null);
            IconButton(root, "CalendarIcon", IconGlyph.Calendar, new Vector2(-578, 434), null, null);

            // ---- 右上角: 时间 + 三种货币 / clock and the three currencies
            Text clock = Label(root, "time", "----/--/-- --:--", 22, new Color(1, 1, 1, 0.72f),
                new Vector2(330, 474));
            ((RectTransform)clock.transform).sizeDelta = new Vector2(420, 34);

            Text coin = Currency(root, "dragonCoin", "LMD", new Vector2(120, 422),
                new Color(0.36f, 0.62f, 0.86f), false);
            Text jade = Currency(root, "syntheticJade", "ORUNDUM", new Vector2(450, 422),
                new Color(0.82f, 0.26f, 0.42f), true);
            Text stone = Currency(root, "sourceStone", "ORIGINITE", new Vector2(780, 422),
                new Color(0.85f, 0.72f, 0.28f), true);

            // ---- 右侧磁贴 / the tile grid, positions traced off the reference at 1920x1080
            GameObject operation = MenuTile(root, "OperationTile", "OPERATION", null,
                new Vector2(508, 253), new Vector2(789, 246), 64, TileLight, TileInk, ui, "SelectDungeonUI");
            AccentBar(operation, false);

            // 标题要让开左边的理智块, 默认是整块磁贴居中, 会压在一起
            // MenuTile centres its caption across the whole tile, which would put OPERATION straight
            // on top of the sanity block; give it just the clear area to the right of it.
            RectTransform operationCaption = (RectTransform)operation.transform.Find("Caption");
            operationCaption.sizeDelta = new Vector2(460, 246);
            operationCaption.anchoredPosition = new Vector2(160, 0);

            // 作战磁贴里那块理智: reason/maxReason 正好是参考图上的 80 和 /123
            // The sanity block inside the operation tile - reason and maxReason are exactly the
            // 80 and /123 the reference prints there.
            GameObject sanity = Rect(operation, "SanityBlock",
                At(new Vector2(-232, 16), new Vector2(286, 190)));
            sanity.AddComponent<Image>().color = new Color(0.72f, 0.73f, 0.75f, 0.55f);
            Label(sanity, "plus", "+", 44, TileInk, new Vector2(-104, 26));
            Text reason = Label(sanity, "reason", "0", 82, TileInk, new Vector2(24, 26));
            ((RectTransform)reason.transform).sizeDelta = new Vector2(210, 110);

            GameObject sanityStrip = Rect(sanity, "SanityStrip",
                At(new Vector2(0, -62), new Vector2(286, 52)));
            sanityStrip.AddComponent<Image>().color = new Color(0.10f, 0.11f, 0.12f, 0.92f);
            Label(sanityStrip, "sanityCaption", "SANITY", 22, new Color(1, 1, 1, 0.75f), new Vector2(-62, 0));
            Label(sanityStrip, "sanitySlash", "/", 24, new Color(1, 1, 1, 0.55f), new Vector2(14, 0));
            Text maxReason = Label(sanityStrip, "maxReason", "0", 26, Color.white, new Vector2(74, 0));
            ((RectTransform)maxReason.transform).sizeDelta = new Vector2(120, 40);

            MenuTile(root, "SquadTile", "SQUAD", null,
                new Vector2(245, 51), new Vector2(337, 130), 40, TileLight, TileInk, ui, "SquadUI");
            MenuTile(root, "OperatorsTile", "OPERATORS", "Roster Management",
                new Vector2(643, 51), new Vector2(417, 130), 40, TileLight, TileInk, ui, "CharUI");

            MenuTile(root, "StoreTile", "STORE", null,
                new Vector2(293, -164), new Vector2(270, 150), 34, TileBlue, Color.white, ui, "ShopUI");
            MenuTile(root, "RecruitTile", "RECRUIT", null,
                new Vector2(574, -112), new Vector2(263, 61), 26, TileLight, TileInk, null, null);
            MenuTile(root, "PublicRecruitTile", "PUBLIC RECRUIT", null,
                new Vector2(552, -198), new Vector2(205, 68), 21, TileBlue, Color.white, null, null);
            MenuTile(root, "HeadhuntTile", "HEADHUNTING", null,
                new Vector2(801, -198), new Vector2(234, 68), 21, TileBlue, Color.white, null, null);

            GameObject missions = MenuTile(root, "MissionsTile", "MISSIONS", null,
                new Vector2(194, -325), new Vector2(219, 130), 36, TileLight, TileInk, null, null);
            AccentBar(missions, true);
            MenuTile(root, "ManufactureTile", "MANUFACTURE", null,
                new Vector2(541, -325), new Vector2(255, 130), 28, TileLight, TileInk, ui, "HouseUI");
            MenuTile(root, "DepotTile", "DEPOT", null,
                new Vector2(848, -349), new Vector2(154, 82), 26, TileLight, TileInk, null, null);

            NewsPanel(root);

            ui.injections = new[] {
                Inject("time", clock),
                Inject("move1", move1.transform),
                Inject("move2", move2.transform),
                Inject("playerName", pName),
                Inject("playerLevel", pLevel),
                Inject("expPercentage", exp),
                Inject("reason", reason),
                Inject("maxReason", maxReason),
                Inject("sourceStone", stone),
                Inject("syntheticJade", jade),
                Inject("dragonCoin", coin),
            };

            SavePrefab(root, "HomeUI");
        }

        private static Injection Inject(string name, Object value) {
            return new Injection { name = name, value = value };
        }

        // ------------------------------------------------------------------ HomeUI parts

        private static System.Action<RectTransform> At(Vector2 centre, Vector2 size) {
            return r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = size;
                r.anchoredPosition = centre;
            };
        }

        private static void Beam(GameObject parent, string name, Vector2 pos, Vector2 size, float alpha) {
            GameObject go = Rect(parent, name, At(pos, size));
            Image img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
        }

        private static void Disc(GameObject parent, string name, Vector2 pos, float diameter, Color color) {
            GameObject go = Rect(parent, name, At(pos, new Vector2(diameter, diameter)));
            Image img = go.AddComponent<Image>();
            img.sprite = Builtin("Knob");
            img.color = color;
            img.raycastTarget = false;
        }

        private static void Tilted(GameObject parent, string name, Vector2 pos, Vector2 size,
                                   float degrees, Color color) {
            GameObject go = Rect(parent, name, At(pos, size));
            go.transform.localRotation = Quaternion.Euler(0, 0, degrees);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private enum IconGlyph { Gear, Alert, Mail, Calendar }

        /// <summary>
        /// 左上角那四个图标: LiberationSans里没有齿轮和信封, 所以用方块和圆拼出来
        /// LiberationSans carries no gear or envelope glyph, so each icon is assembled out of a
        /// couple of primitives. Abstract, but it never renders as a missing-glyph box - which is
        /// what putting U+2699 in a Text here would actually do.
        /// </summary>
        private static void IconButton(GameObject parent, string name, IconGlyph glyph, Vector2 pos,
                                       RuaUI ui, string screen) {
            GameObject go = Rect(parent, name, At(pos, new Vector2(58, 58)));
            Image img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.10f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = img;

            switch (glyph) {
                case IconGlyph.Gear:
                    Disc(go, "Rim", Vector2.zero, 40, new Color(1, 1, 1, 0.85f));
                    Disc(go, "Bore", Vector2.zero, 15, HomeInk);
                    break;
                case IconGlyph.Alert:
                    Tilted(go, "Diamond", Vector2.zero, new Vector2(34, 34), 45f, new Color(1, 1, 1, 0.85f));
                    Label(go, "Bang", "!", 24, HomeInk, Vector2.zero);
                    break;
                case IconGlyph.Mail:
                    Beam(go, "Body", Vector2.zero, new Vector2(44, 30), 0.85f);
                    Tilted(go, "FlapLeft", new Vector2(-6, 4), new Vector2(26, 3), -34f, HomeInk);
                    Tilted(go, "FlapRight", new Vector2(6, 4), new Vector2(26, 3), 34f, HomeInk);
                    break;
                case IconGlyph.Calendar:
                    // 网格要画得不对称, 居中的十字看着就是个加号
                    // The grid has to be off-centre: a centred cross just reads as a plus sign.
                    Beam(go, "Body", Vector2.zero, new Vector2(42, 36), 0.85f);
                    Tilted(go, "Head", new Vector2(0, 14), new Vector2(42, 8), 0f, new Color(1, 1, 1, 0.45f));
                    Tilted(go, "RowLine", new Vector2(0, -2), new Vector2(32, 2), 0f, HomeInk);
                    Tilted(go, "Col1", new Vector2(-10, -8), new Vector2(2, 14), 0f, HomeInk);
                    Tilted(go, "Col2", new Vector2(10, -8), new Vector2(2, 14), 0f, HomeInk);
                    break;
            }

            if (ui != null && screen != null) Wire(button, ui, screen);
        }

        /// <summary>
        /// 右上角一格货币: 图标 + 数值(注入) + 加号 / one currency slot: icon, injected value, plus.
        /// </summary>
        private static Text Currency(GameObject parent, string name, string caption, Vector2 pos,
                                     Color iconColor, bool plus) {
            GameObject slot = Rect(parent, name + "Slot", At(pos, new Vector2(280, 56)));

            Tilted(slot, "Icon", new Vector2(-118, 0), new Vector2(30, 30), 45f, iconColor);
            Text captionText = Label(slot, name + "Caption", caption, 13,
                new Color(1, 1, 1, 0.45f), new Vector2(-118, -30));
            ((RectTransform)captionText.transform).sizeDelta = new Vector2(140, 20);

            // 数值右对齐: Lua随时会把它从"5"改成"10000", 左对齐的话加号和数字之间的空隙会忽大忽小
            // The value is right-aligned against a fixed edge. Lua rewrites it from "5" to "10000"
            // at will, and left-aligning would leave the plus button stranded a different distance
            // away for every length; this way it always sits tight against the last digit.
            Text value = Label(slot, name, "0", 30, Color.white, new Vector2(-40, 0));
            RectTransform valueRect = (RectTransform)value.transform;
            valueRect.sizeDelta = new Vector2(160, 44);
            value.alignment = TextAnchor.MiddleRight;

            if (plus) {
                Disc(slot, "Plus", new Vector2(68, 0), 32, new Color(1, 1, 1, 0.22f));
                Label(slot, "PlusGlyph", "+", 25, Color.white, new Vector2(68, 0));
            }
            return value;
        }

        /// <summary>
        /// 斜切磁贴 / A leaning tile.
        /// 斜边放在九宫格的左右边框里, 所以磁贴拉宽时斜度不变; 文字是独立的正的子节点
        /// The slant lives in the sprite's left and right 9-slice borders, so widening a tile keeps
        /// the lean at a constant width rather than shearing the whole shape further. The caption is
        /// a separate upright child - sheared text is unreadable, and the reference keeps it upright
        /// too.
        /// </summary>
        private static GameObject MenuTile(GameObject parent, string name, string caption, string subtitle,
                                           Vector2 centre, Vector2 size, int fontSize, Color fill,
                                           Color textColor, RuaUI ui, string screen) {
            GameObject go = Rect(parent, name, At(centre, size));
            Image img = go.AddComponent<Image>();
            img.sprite = skewTile;
            img.type = Image.Type.Sliced;
            img.color = fill;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = img;

            Text label = Label(go, "Caption", caption, fontSize, textColor,
                new Vector2(0, subtitle == null ? 0 : 13));
            ((RectTransform)label.transform).sizeDelta = new Vector2(size.x - TileSkew * 2f, size.y);

            if (subtitle != null) {
                Text sub = Label(go, "Subtitle", subtitle, 19,
                    new Color(textColor.r, textColor.g, textColor.b, 0.62f), new Vector2(0, -26));
                ((RectTransform)sub.transform).sizeDelta = new Vector2(size.x - TileSkew * 2f, 30);
            }

            if (ui != null && screen != null) Wire(button, ui, screen);
            return go;
        }

        /// <summary>
        /// 把按钮接到 UIBase.Show(string) 上 / points a button at UIBase.Show(string).
        ///
        /// onClick.AddListener 加的是运行时监听, 存不进预制体; 要写进序列化数据必须用 UnityEventTools。
        /// AddListener registers a RUNTIME listener, which is not serialised into the prefab and
        /// would silently vanish - persistent listeners are the only kind that survive, and they
        /// need a Component target, which is why this goes through UIBase.Show(string) on the
        /// screen's own RuaUI rather than UIManager (a plain C# singleton, not a Component).
        ///
        /// 目标界面的预制体还不存在, 点了只会打一条缺失资源的错误, 不会崩
        /// The target prefabs do not exist yet, so a click logs one missing-prefab error from
        /// UIManager.Load and returns null. Nothing throws.
        /// </summary>
        private static void Wire(Button button, RuaUI ui, string screen) {
            UnityEventTools.AddStringPersistentListener(button.onClick, ui.Show, screen);
        }

        /// <summary>磁贴上的橙色细条 / the orange rule the reference draws on some tiles.</summary>
        private static void AccentBar(GameObject tile, bool top) {
            GameObject go = Rect(tile, top ? "AccentTop" : "AccentBottom", r => {
                r.anchorMin = new Vector2(0, top ? 1 : 0);
                r.anchorMax = new Vector2(1, top ? 1 : 0);
                r.pivot = new Vector2(0.5f, top ? 1 : 0);
                r.sizeDelta = new Vector2(-TileSkew * 2f, 5f);
                r.anchoredPosition = new Vector2(0, top ? -2f : 2f);
            });
            Image img = go.AddComponent<Image>();
            img.color = Accent;
            img.raycastTarget = false;
        }

        /// <summary>左下角的新闻小屏 / the breaking-news screen in the bottom-left corner.</summary>
        private static void NewsPanel(GameObject parent) {
            GameObject panel = Rect(parent, "NewsPanel", At(new Vector2(-832, -335), new Vector2(256, 164)));
            panel.AddComponent<Image>().color = new Color(0.09f, 0.10f, 0.11f, 0.92f);

            GameObject strip = Rect(panel, "BreakingLabel", At(new Vector2(-38, 62), new Vector2(180, 26)));
            strip.AddComponent<Image>().color = new Color(0.80f, 0.10f, 0.14f);
            Text breaking = Label(strip, "BreakingText", "BREAKING NEWS", 16, Color.white, Vector2.zero);
            ((RectTransform)breaking.transform).sizeDelta = new Vector2(180, 26);

            // 彩条测试图 / the colour-bar test pattern on the little screen
            Color[] bars = {
                new Color(0.75f, 0.75f, 0.75f), new Color(0.75f, 0.75f, 0.20f),
                new Color(0.20f, 0.75f, 0.75f), new Color(0.20f, 0.70f, 0.25f),
                new Color(0.75f, 0.25f, 0.70f), new Color(0.75f, 0.20f, 0.22f),
                new Color(0.22f, 0.25f, 0.72f)
            };
            for (int i = 0; i < bars.Length; i++) {
                GameObject bar = Rect(panel, "Bar" + i, At(new Vector2(-105 + i * 30, 6), new Vector2(28, 74)));
                Image barImage = bar.AddComponent<Image>();
                barImage.color = bars[i];
                barImage.raycastTarget = false;
            }

            Text stand = Label(panel, "StandBy", "PLEASE STAND BY", 14,
                new Color(1, 1, 1, 0.65f), new Vector2(0, -58));
            ((RectTransform)stand.transform).sizeDelta = new Vector2(256, 24);
        }

        // ------------------------------------------------------------------ PlayerData

        /// <summary>
        /// PlayerData.Initialization 已经把等级/理智/物品(0,1,2)都填好了, 正好是HomeUI.lua需要的
        /// PlayerData.Initialization already seeds level, reason and items 0/1/2 - exactly what
        /// HomeUI.lua reads - so there is no need to poke private fields via SerializedObject.
        /// </summary>
        private static void BuildPlayerData(string userName, string password) {
            string path = UserDataDir + "/" + userName + ".asset";
            PlayerData data = ScriptableObject.CreateInstance<PlayerData>();
            data.Initialization(userName, password);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);
        }

        // ------------------------------------------------------------------ UI helpers

        private static GameObject NewUIRoot(string name) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform r = (RectTransform)go.transform;
            Stretch(r);
            go.AddComponent<CanvasGroup>(); // UIBase.Awake 读取 / UIBase.Awake reads this
            return go;
        }

        private static void Stretch(RectTransform r) {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static System.Action<RectTransform> Centered(float w, float h) {
            return r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(w, h);
                r.anchoredPosition = Vector2.zero;
            };
        }

        private static GameObject Rect(GameObject parent, string name, System.Action<RectTransform> layout) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            layout((RectTransform)go.transform);
            return go;
        }

        private static GameObject GroupGo(GameObject parent, string name,
                                          System.Action<RectTransform> layout, Color color, float alpha) {
            GameObject go = Rect(parent, name, layout);
            go.AddComponent<Image>().color = color;
            go.AddComponent<CanvasGroup>().alpha = alpha;
            return go;
        }

        private static CanvasGroup Group(GameObject parent, string name,
                                         System.Action<RectTransform> layout, Color color, float alpha) {
            return GroupGo(parent, name, layout, color, alpha).GetComponent<CanvasGroup>();
        }

        private static System.Action<RectTransform> Inset(float margin) {
            return r => {
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(margin, margin);
                r.offsetMax = new Vector2(-margin, -margin);
            };
        }

        private static GameObject ChamferFill(GameObject parent, string name,
                                              System.Action<RectTransform> layout, Color color,
                                              Sprite sprite = null) {
            GameObject go = Rect(parent, name, layout);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : chamferBig;
            img.type = Image.Type.Sliced;
            img.color = color;
            return go;
        }

        private static CanvasGroup ChamferGroup(GameObject parent, string name,
                                                System.Action<RectTransform> layout, Color color,
                                                float alpha, Sprite sprite = null) {
            CanvasGroup group = ChamferFill(parent, name, layout, color, sprite).AddComponent<CanvasGroup>();
            group.alpha = alpha;
            return group;
        }

        /// <summary>
        /// 控制台面板: 橙色切角外框 + 内缩的切角填充层, 边缘露出一条细橙线, 四角加HUD托架
        /// A console panel: an accent chamfered frame with an inset chamfered fill, so a thin
        /// accent line shows around the edge, plus optional HUD corner brackets. Returns the
        /// fill - children attach there, exactly like the GroupGo callers expect - while the
        /// CanvasGroup comes back on the frame, which is what Show/Hide toggles.
        /// </summary>
        private static GameObject FramePanel(GameObject parent, string name,
                                             System.Action<RectTransform> layout, Color fillColor,
                                             float alpha, bool brackets, out CanvasGroup group) {
            GameObject frame = Rect(parent, name, layout);
            Image frameImg = frame.AddComponent<Image>();
            frameImg.sprite = chamferBig;
            frameImg.type = Image.Type.Sliced;
            frameImg.color = Accent;
            group = frame.AddComponent<CanvasGroup>();
            group.alpha = alpha;

            GameObject fill = Rect(frame, name + "Fill", Inset(FrameGap));
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = chamferBig;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = fillColor;

            // 托架要在填充层之后创建, 否则会被盖住 / after the fill, or the fill covers them
            if (brackets) CornerBrackets(frame, ChamferBigCorner + 10f);
            return fill;
        }

        private static void CornerBrackets(GameObject panel, float inset) {
            Vector2[] corners = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            foreach (Vector2 anchor in corners) CornerBracket(panel, anchor, inset);
        }

        /// <summary>
        /// 轴心跟着锚点走, 四个角就自动镜像, 不用分别算方向
        /// Pinning the pivot to the same corner as the anchor mirrors the L automatically, so
        /// all four corners fall out of one set of numbers.
        /// </summary>
        private static void CornerBracket(GameObject parent, Vector2 anchor, float inset) {
            const float armLength = 24f;
            const float thickness = 3f;
            Vector2 pivot = new Vector2(anchor.x < 0.5f ? 0f : 1f, anchor.y < 0.5f ? 0f : 1f);
            Vector2 pos = new Vector2(anchor.x < 0.5f ? inset : -inset, anchor.y < 0.5f ? inset : -inset);

            foreach (Vector2 size in new[] { new Vector2(armLength, thickness), new Vector2(thickness, armLength) }) {
                GameObject arm = Rect(parent, "Bracket", r => {
                    r.anchorMin = r.anchorMax = anchor;
                    r.pivot = pivot;
                    r.sizeDelta = size;
                    r.anchoredPosition = pos;
                });
                arm.AddComponent<Image>().color = Accent;
            }
        }

        private static Text TitleLabel(GameObject parent, string name, string text, int size,
                                       Vector2 pos, float ruleWidth) {
            Text t = Label(parent, name, text, size, Accent, pos);
            GameObject rule = Rect(parent, name + "Rule", r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(ruleWidth, 3f);
                r.anchoredPosition = pos + new Vector2(0, -size * 0.85f);
            });
            rule.AddComponent<Image>().color = Accent;
            return t;
        }

        private static Text Label(GameObject parent, string name, string text, int size, Color color, Vector2 pos) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(700, 60);
                r.anchoredPosition = pos;
            });
            Text t = go.AddComponent<Text>();
            Style(t, size, color);
            t.text = text;
            return t;
        }

        private static void Style(Text t, int size, Color color) {
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
        }

        private static Button Btn(GameObject parent, string name, string caption, Vector2 pos) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(240, 60);
                r.anchoredPosition = pos;
            });
            Image img = go.AddComponent<Image>();
            img.sprite = chamferSmall;
            img.type = Image.Type.Sliced;
            img.color = Accent;
            Button b = go.AddComponent<Button>();
            b.targetGraphic = img;

            // Selectable 运行时会用 colors.normalColor 覆盖 targetGraphic 的颜色, 只设 img.color
            // 的话按钮一进Play就变白, 所以配色必须同时写进 ColorBlock
            // Selectable overwrites targetGraphic's colour with colors.normalColor at runtime,
            // so setting img.color alone turns the button white the moment Play starts - the
            // tint has to go on the ColorBlock too.
            ColorBlock colors = b.colors;
            colors.normalColor = Accent;
            colors.highlightedColor = Color.Lerp(Accent, Color.white, 0.25f);
            colors.pressedColor = AccentDim;
            colors.selectedColor = Accent;
            colors.disabledColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            colors.fadeDuration = 0.1f;
            b.colors = colors;

            Text t = Label(go, "Text", caption, 24, Ink, Vector2.zero);
            ((RectTransform)t.transform).sizeDelta = new Vector2(240, 60);
            return b;
        }

        /// <summary>
        /// 旧版InputField必须有textComponent, 否则输入时会空引用
        /// A legacy InputField without a textComponent throws as soon as it is used.
        /// </summary>
        private static InputField Input(GameObject parent, string name, string placeholder, Vector2 pos) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(460, 56);
                r.anchoredPosition = pos;
            });
            Image img = go.AddComponent<Image>();
            img.sprite = chamferSmall;
            img.type = Image.Type.Sliced;
            img.color = Field;

            GameObject underline = Rect(go, "Underline", r => {
                r.anchorMin = new Vector2(0, 0);
                r.anchorMax = new Vector2(1, 0);
                r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = new Vector2(0, 2f);
                r.anchoredPosition = Vector2.zero;
            });
            underline.AddComponent<Image>().color = Accent;

            GameObject textGo = Rect(go, "Text", r => {
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(16, 8);
                r.offsetMax = new Vector2(-16, -8);
            });
            Text text = textGo.AddComponent<Text>();
            Style(text, 24, Color.white);
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            GameObject phGo = Rect(go, "Placeholder", r => {
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(16, 8);
                r.offsetMax = new Vector2(-16, -8);
            });
            Text ph = phGo.AddComponent<Text>();
            Style(ph, 24, new Color(1, 1, 1, 0.35f));
            ph.alignment = TextAnchor.MiddleLeft;
            ph.text = placeholder;

            InputField field = go.AddComponent<InputField>();
            field.targetGraphic = img;
            field.textComponent = text;
            field.placeholder = ph;

            // 和按钮同理: 不写 ColorBlock 输入框运行时会变白 / same tint trap as the buttons
            ColorBlock colors = field.colors;
            colors.normalColor = Field;
            colors.highlightedColor = Color.Lerp(Field, Color.white, 0.12f);
            colors.pressedColor = Field;
            colors.selectedColor = Color.Lerp(Field, Accent, 0.15f);
            colors.disabledColor = new Color(Field.r, Field.g, Field.b, 0.5f);
            colors.fadeDuration = 0.1f;
            field.colors = colors;
            return field;
        }

        private static Toggle Check(GameObject parent, string name, string caption, Vector2 pos) {
            GameObject go = Rect(parent, name, r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(460, 40);
                r.anchoredPosition = pos;
            });
            GameObject boxGo = Rect(go, "Background", r => {
                r.anchorMin = r.anchorMax = new Vector2(0, 0.5f);
                r.pivot = new Vector2(0, 0.5f);
                r.sizeDelta = new Vector2(30, 30);
                r.anchoredPosition = Vector2.zero;
            });
            // 30像素的小方框不切角 - 13像素的切角会把它削成八边形
            // The 30px box stays square: a 13px chamfer would eat it into an octagon.
            Image box = boxGo.AddComponent<Image>();
            box.color = Field;

            GameObject markGo = Rect(boxGo, "Checkmark", Stretch);
            Image mark = markGo.AddComponent<Image>();
            mark.sprite = Builtin("Checkmark");
            mark.color = Accent;

            Text t = Label(go, "Label", caption, 20, Color.white, new Vector2(30, 0));
            ((RectTransform)t.transform).sizeDelta = new Vector2(400, 40);
            t.alignment = TextAnchor.MiddleLeft;

            Toggle toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = mark;
            toggle.isOn = false;

            ColorBlock colors = toggle.colors;
            colors.normalColor = Field;
            colors.highlightedColor = Color.Lerp(Field, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(Field, Color.black, 0.2f);
            colors.selectedColor = Field;
            colors.disabledColor = new Color(Field.r, Field.g, Field.b, 0.5f);
            colors.fadeDuration = 0.1f;
            toggle.colors = colors;
            return toggle;
        }

        // ------------------------------------------------------------------ asset plumbing

        // ------------------------------------------------------------------ Game prefabs

        /// <summary>
        /// GameManager.Initialization 加载的四个战斗预制体 / The four battle prefabs GameManager loads.
        ///
        /// Entity's constructor does transform.Find("Model"), then Model.Find("Skeleton")
        /// .GetComponent&lt;SkeletonAnimation&gt;() (via the Expand.GetComponent extension), and Char
        /// additionally reads a "Dir" child. The placeholders reproduce that hierarchy exactly, so
        /// real Spine assets drop in without the prefabs being rebuilt.
        ///
        /// 这些只是占位的方块, 骨骼数据来自缺失的 Meta/Char - 真资源到位前跑不了战斗
        /// The quads are stand-ins only: skeletons come from the absent Meta/Char and Meta/Monster
        /// bundles, so battle still cannot run - these exist to stop GameManager erroring on boot
        /// and to give the scene something visible once it can.
        ///
        /// SkeletonAnimation is [ExecuteInEditMode], so it initialises the moment it is added here.
        /// That is silent: SkeletonRenderer.Initialize returns early on a null skeletonDataAsset
        /// (SkeletonRenderer.cs:336) rather than logging, so generation stays quiet.
        /// </summary>
        private static void BuildCharPrefab() {
            GameObject root = new GameObject("CharPrefab");
            EntityBody(root, Accent, 1.6f);

            // Char.dir: 决定朝向和攻击范围的原点 / drives facing and is the attack-range origin
            GameObject dir = new GameObject("Dir");
            dir.transform.SetParent(root.transform, false);

            SavePrefab(root, GamePrefabDir, "CharPrefab");
        }

        private static void BuildMonsterPrefab() {
            GameObject root = new GameObject("MonsterPrefab");
            EntityBody(root, Hostile, 1.4f);
            SavePrefab(root, GamePrefabDir, "MonsterPrefab");
        }

        private static void EntityBody(GameObject root, Color color, float height) {
            // Entity.Update 对 Model 的 z 做缩放来翻转朝向, 占位方块跟着翻正好
            // Entity.Update flips Model's z scale to face the target; the stand-in rides along.
            GameObject model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);

            GameObject skeleton = new GameObject("Skeleton");
            skeleton.transform.SetParent(model.transform, false);
            skeleton.AddComponent<SkeletonAnimation>();

            GameObject body = Primitive(PrimitiveType.Quad, model.transform, "Placeholder");
            body.transform.localScale = new Vector3(height * 0.5f, height, 1f);
            body.transform.localPosition = new Vector3(0, height * 0.5f, 0);
            Paint(body, "M_Placeholder" + root.name, color, false);
        }

        /// <summary>
        /// GameUI:354 放置干员时生成的落点标记 / the deploy marker GameUI spawns at GameUI.cs:354.
        /// </summary>
        private static void BuildCharPlacePrefab() {
            GameObject root = new GameObject("CharPlacePrefab");
            GroundTile(root, "M_PlaceholderCharPlace", Accent, 0.92f, 0.45f);
            SavePrefab(root, GamePrefabDir, "CharPlacePrefab");
        }

        /// <summary>
        /// GameUI:278 每格攻击范围生成一个 / GameUI spawns one of these per attack-range cell.
        /// </summary>
        private static void BuildAtkRangeDisplayPrefab() {
            GameObject root = new GameObject("AtkRangeDisplay");
            GroundTile(root, "M_PlaceholderAtkRange", Ranged, 0.90f, 0.35f);
            SavePrefab(root, GamePrefabDir, "AtkRangeDisplay");
        }

        /// <summary>
        /// 平铺在地面的半透明格子 / A translucent cell lying flat on the ground.
        /// 调用方会自带 rotation, 所以根节点保持不旋转, 由子物体负责躺平
        /// Both callers pass their own rotation to Instantiate, so the root is left unrotated and
        /// the child does the lying-flat - otherwise the caller's rotation would stand it upright.
        /// </summary>
        private static void GroundTile(GameObject root, string materialName, Color color, float size, float alpha) {
            GameObject tile = Primitive(PrimitiveType.Quad, root.transform, "Tile");
            tile.transform.localRotation = Quaternion.Euler(90, 0, 0);
            tile.transform.localScale = new Vector3(size, size, 1f);
            Paint(tile, materialName, new Color(color.r, color.g, color.b, alpha), true);
        }

        /// <summary>
        /// CreatePrimitive 会附带碰撞体, 必须去掉: GameUI 用射线点地面选格子, 多一个碰撞体就会挡住
        /// CreatePrimitive attaches a collider. It has to go - GameUI raycasts the ground to pick
        /// cells, and a stray collider on a marker would intercept those hits and change gameplay.
        /// </summary>
        private static GameObject Primitive(PrimitiveType type, Transform parent, string name) {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return go;
        }

        /// <summary>
        /// 内置渲染管线, 所以 Unlit/Color 和 Sprites/Default 都在 / Built-in RP: both shaders ship with it.
        /// Sprites/Default is the transparent one - it tints by _Color over a white default texture.
        /// </summary>
        private static void Paint(GameObject go, string materialName, Color color, bool transparent) {
            string path = GamePrefabDir + "/" + materialName + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {
                mat = new Material(Shader.Find(transparent ? "Sprites/Default" : "Unlit/Color"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ asset plumbing

        private static Sprite Builtin(string name) {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + name + ".psd");
        }

        /// <summary>
        /// 画一张四角45度切角的九宫格贴图 - 明日方舟面板的招牌造型, 没有美术资源就用代码画
        ///
        /// Draws the 45-degree "cut corner" panel shape Arknights UI is built on, as a 9-sliced
        /// sprite. Unity ships no chamfered built-in, and this repo has no art, so it is
        /// rasterised here. PPU must match the canvas referencePixelsPerUnit (100): uGUI scales
        /// the 9-slice border by referencePixelsPerUnit/spritePPU, so a "helpful" PPU of 1 blows
        /// the 28px border up to 2800px, Unity clamps it to the rect, and every panel renders as
        /// a diamond. At 100 one texture pixel is one canvas unit and the bevel stays a constant
        /// on-screen size however far the panel is stretched.
        /// </summary>
        private static Sprite BuildChamferSprite(string name, int size, int corner) {
            const int ss = 2; // 2倍超采样让斜边不锯齿 / 2x supersample to smooth the diagonals
            int hiSize = size * ss;
            int hiCorner = corner * ss;

            bool[] hi = new bool[hiSize * hiSize];
            for (int y = 0; y < hiSize; y++) {
                for (int x = 0; x < hiSize; x++) {
                    hi[y * hiSize + x] = InsideChamfer(x, y, hiSize, hiCorner);
                }
            }

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    int hits = 0;
                    for (int dy = 0; dy < ss; dy++) {
                        for (int dx = 0; dx < ss; dx++) {
                            if (hi[(y * ss + dy) * hiSize + (x * ss + dx)]) hits++;
                        }
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, (float)hits / (ss * ss));
                }
            }

            return ImportSprite(name, pixels, size, corner);
        }

        /// <summary>
        /// 画那个挂在顶上的红色线框球 / Draws the red wireframe ball every reference screen hangs
        /// from the top. 没有美术资源, 所以点用黄金角铺在球面上, 各自连最近的几个邻居, 再正交投影
        /// With no art to load, the vertices are spread over a sphere by the golden angle - an even
        /// scatter without having to subdivide a real icosahedron - then each is linked to its
        /// nearest neighbours and projected straight down the z axis. Edges are drawn as chords
        /// rather than arcs, which is what a faceted polyhedron actually looks like, and depth
        /// fades the far half so the result reads as a ball instead of a flat doily.
        /// </summary>
        private static Sprite BuildWireSphereSprite(string name, int size) {
            const int pointCount = 54;
            const int linksPerPoint = 4;

            Vector3[] points = new Vector3[pointCount];
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < pointCount; i++) {
                float y = 1f - i / (float)(pointCount - 1) * 2f;
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float theta = goldenAngle * i;
                points[i] = new Vector3(Mathf.Cos(theta) * ring, y, Mathf.Sin(theta) * ring);
            }

            Color[] pixels = new Color[size * size];
            float half = size * 0.5f;
            float radius = half * 0.88f;

            int[] order = new int[pointCount];
            for (int i = 0; i < pointCount; i++) {
                int self = i;
                for (int j = 0; j < pointCount; j++) order[j] = j;
                System.Array.Sort(order, (a, b) => (points[self] - points[a]).sqrMagnitude
                    .CompareTo((points[self] - points[b]).sqrMagnitude));
                // order[0] 是自己 / order[0] is the point itself, at distance zero
                for (int k = 1; k <= linksPerPoint && k < pointCount; k++) {
                    WireEdge(pixels, size, points[i], points[order[k]], half, radius);
                }
            }

            for (int i = 0; i < pointCount; i++) WireNodeDot(pixels, size, points[i], half, radius);

            return ImportSprite(name, pixels, size, 0);
        }

        private static void WireEdge(Color[] pixels, int size, Vector3 a, Vector3 b,
                                     float half, float radius) {
            int steps = Mathf.Max(1, Mathf.CeilToInt((a - b).magnitude * radius));
            for (int s = 0; s <= steps; s++) {
                Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
                float alpha = Mathf.Lerp(0.16f, 0.95f, Mathf.InverseLerp(-1f, 1f, p.z));
                Plot(pixels, size, half + p.x * radius, half + p.y * radius, WireLine, alpha);
            }
        }

        private static void WireNodeDot(Color[] pixels, int size, Vector3 p, float half, float radius) {
            float depth = Mathf.InverseLerp(-1f, 1f, p.z);
            float peak = Mathf.Lerp(0.25f, 1f, depth);
            float dotRadius = Mathf.Lerp(2.0f, 4.5f, depth);
            int cx = Mathf.RoundToInt(half + p.x * radius);
            int cy = Mathf.RoundToInt(half + p.y * radius);
            int span = Mathf.CeilToInt(dotRadius);

            for (int dy = -span; dy <= span; dy++) {
                for (int dx = -span; dx <= span; dx++) {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > dotRadius) continue;
                    Splat(pixels, size, cx + dx, cy + dy, WireNode, peak * (1f - d / dotRadius));
                }
            }
        }

        /// <summary>双线性铺开一个点, 不然斜线全是锯齿 / spreads a sample over the four pixels it straddles.</summary>
        private static void Plot(Color[] pixels, int size, float x, float y, Color color, float alpha) {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            Splat(pixels, size, x0, y0, color, alpha * (1f - fx) * (1f - fy));
            Splat(pixels, size, x0 + 1, y0, color, alpha * fx * (1f - fy));
            Splat(pixels, size, x0, y0 + 1, color, alpha * (1f - fx) * fy);
            Splat(pixels, size, x0 + 1, y0 + 1, color, alpha * fx * fy);
        }

        /// <summary>
        /// 取最亮的一次写入 / keeps the brightest contributor.
        /// 每条边会被两端各画一次, 直接叠加会让交叉点糊成一团, 取最大值就没这问题
        /// Every edge gets drawn twice, once from each end, and nodes sit on top of edges. Adding
        /// those up blows the crossings out into blobs; taking the max keeps the line weight even.
        /// </summary>
        private static void Splat(Color[] pixels, int size, int x, int y, Color color, float alpha) {
            if (x < 0 || y < 0 || x >= size || y >= size || alpha <= 0f) return;
            int index = y * size + x;
            if (alpha <= pixels[index].a) return;
            pixels[index] = new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>
        /// 写PNG进工程再按Sprite导入 / writes the PNG into the project and imports it as a Sprite.
        /// PPU 必须是100, 和画布的 referencePixelsPerUnit 对上, 否则九宫格边框会被放大到整块塌掉
        /// PPU has to stay at the canvas's referencePixelsPerUnit of 100: uGUI scales the 9-slice
        /// border by referencePixelsPerUnit/spritePPU, so any other value distorts the corners.
        /// </summary>
        /// <summary>
        /// 画一个向右倾的平行四边形九宫格 / the leaning parallelogram behind every home-screen tile.
        /// 斜度只放进左右边框, 上下边框留0: 上下要是也切片, 每一行的形状就被固定住了, 平行四边形会塌掉
        /// The slant goes into the left and right borders only, with top and bottom left at zero.
        /// Slicing vertically as well would pin the top and bottom rows to a fixed height, and since
        /// this shape changes with every row, that collapses the parallelogram into a stepped mess.
        /// </summary>
        private static Sprite BuildSkewSprite(string name, int size, int slant) {
            const int ss = 2; // 2倍超采样 / 2x supersample, same as the chamfer
            int hiSize = size * ss;
            int hiSlant = slant * ss;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    int hits = 0;
                    for (int dy = 0; dy < ss; dy++) {
                        for (int dx = 0; dx < ss; dx++) {
                            int hx = x * ss + dx;
                            int hy = y * ss + dy;
                            float offset = hiSlant * (hy / (float)(hiSize - 1));
                            if (hx >= offset && hx < hiSize - hiSlant + offset) hits++;
                        }
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, (float)hits / (ss * ss));
                }
            }
            return ImportSprite(name, pixels, size, new Vector4(slant, 0, slant, 0));
        }

        /// <summary>
        /// 画一个圆环 / an annulus - the level ring and the exp arc drawn over it.
        /// </summary>
        private static Sprite BuildRingSprite(string name, int size, float innerRatio) {
            Color[] pixels = new Color[size * size];
            float half = size * 0.5f;
            float outer = half - 1f;
            float inner = outer * innerRatio;

            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 内外两条边各留1像素做抗锯齿 / one pixel of feather on each edge
                    float alpha = Mathf.Clamp01(Mathf.Min(outer - d, d - inner));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            return ImportSprite(name, pixels, size, Vector4.zero);
        }

        private static Sprite ImportSprite(string name, Color[] pixels, int size, int border) {
            return ImportSprite(name, pixels, size, new Vector4(border, border, border, border));
        }

        private static Sprite ImportSprite(string name, Color[] pixels, int size, Vector4 border) {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            string assetPath = UIPrefabDir + "/" + name + ".png";
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath),
                tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        /// <summary>
        /// 四个角对称, 所以贴图正反都一样, 不用管纹理坐标的原点在哪
        /// All four corners are cut identically, so the texture reads the same either way up -
        /// no need to care which end of the pixel array is the bottom row.
        /// </summary>
        private static bool InsideChamfer(int x, int y, int size, int corner) {
            int left = x;
            int right = size - 1 - x;
            int bottom = y;
            int top = size - 1 - y;
            if (left + bottom < corner) return false;
            if (right + bottom < corner) return false;
            if (left + top < corner) return false;
            if (right + top < corner) return false;
            return true;
        }

        private static Material PlaceholderMaterial() {
            const string path = UIPrefabDir + "/PlaceholderBlur.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("UI/Default"));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void SavePrefab(GameObject root, string name) {
            SavePrefab(root, UIPrefabDir, name);
        }

        private static void SavePrefab(GameObject root, string dir, string name) {
            string path = dir + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path) {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
