using System.Collections.Generic;
using System.IO;
using Data.Player;
using Tools;
using UI;
using UI.Sub;
using UnityEditor;
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

        private const float ChamferBigCorner = 28f;
        private const float ChamferSmallCorner = 13f;
        private const float FrameGap = 5f;

        private static Font font;
        private static Sprite chamferBig;
        private static Sprite chamferSmall;

        [MenuItem("Arknights/Placeholders/Generate Bootstrap Assets", false, 0)]
        public static void Generate() {
            font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            EnsureFolder(UIPrefabDir);
            EnsureFolder(UserDataDir);

            chamferBig = BuildChamferSprite("T_ChamferBig", 160, (int)ChamferBigCorner);
            chamferSmall = BuildChamferSprite("T_ChamferSmall", 80, (int)ChamferSmallCorner);

            BuildCameraPrefab();
            BuildLoginUIPrefab();
            BuildCommonDialogPrefab();
            BuildHomeUIPrefab();
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
            foreach (string dir in new[] { UIPrefabDir, ResourcesRoot + "/Data" }) {
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

            ui.backGround = Group(root, "BackGround", Stretch, new Color(0, 0, 0, 0.6f), 0f);

            // sphere 被 DOAnchorPosY / DOScale 驱动 / driven by DOAnchorPosY and DOScale
            GameObject sphereGo = Rect(root, "Sphere", r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(420, 420);
                r.anchoredPosition = new Vector2(0, 263);
            });
            Image sphereImg = sphereGo.AddComponent<Image>();
            sphereImg.color = new Color(0.74f, 0.36f, 0.36f, 0.85f);
            sphereImg.sprite = Builtin("Knob");
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

            // ---- home panel
            GameObject home = FramePanel(root, "HomePanel", Centered(560, 300), Panel, 1f, true,
                out CanvasGroup homeGroup);
            ui.homePanel = homeGroup;
            TitleLabel(home, "Title", "ARKNIGHTS", 44, new Vector2(0, 92), 220);
            ui.home_login = Btn(home, "home_login", "登 录  Login", new Vector2(0, 10));
            ui.home_register = Btn(home, "home_register", "注 册  Register", new Vector2(0, -70));

            // ---- login panel
            GameObject login = FramePanel(root, "LoginPanel", Centered(620, 400), Panel, 0f, true,
                out CanvasGroup loginGroup);
            ui.loginPanel = loginGroup;
            loginGroup.gameObject.SetActive(false);
            TitleLabel(login, "Title", "登录 / Login", 32, new Vector2(0, 150), 180);
            ui.login_userName = Input(login, "login_userName", "用户名 Username", new Vector2(0, 70));
            ui.login_password = Input(login, "login_password", "密码 Password", new Vector2(0, 0));
            ui.login_password.contentType = InputField.ContentType.Password;
            ui.login_enter = Btn(login, "login_enter", "进 入", new Vector2(-130, -100));
            ui.login_back = Btn(login, "login_back", "返 回", new Vector2(130, -100));

            // ---- register panel
            GameObject reg = FramePanel(root, "RegisterPanel", Centered(620, 520), Panel, 0f, true,
                out CanvasGroup regGroup);
            ui.registerPanel = regGroup;
            regGroup.gameObject.SetActive(false);
            TitleLabel(reg, "Title", "注册 / Register", 32, new Vector2(0, 210), 200);
            ui.register_userName = Input(reg, "register_userName", "用户名 (3+)", new Vector2(0, 140));
            ui.register_password = Input(reg, "register_password", "密码 (6-18)", new Vector2(0, 70));
            ui.register_password.contentType = InputField.ContentType.Password;
            ui.register_rePassword = Input(reg, "register_rePassword", "确认密码", new Vector2(0, 0));
            ui.register_rePassword.contentType = InputField.ContentType.Password;
            ui.register_toggle = Check(reg, "register_toggle", "同意注册协议与隐私协议", new Vector2(0, -70));
            ui.register_enter = Btn(reg, "register_enter", "确 认", new Vector2(-130, -160));
            ui.register_back = Btn(reg, "register_back", "返 回", new Vector2(130, -160));

            // ---- loading panel: bars are driven through anchorMin.x / anchorMax.x
            GameObject loading = GroupGo(root, "LoadingPanel", Stretch, new Color(0, 0, 0, 0.85f), 0f);
            ui.loadingPanel = loading.GetComponent<CanvasGroup>();
            loading.SetActive(false);
            ui.loading_bar1 = Bar(loading, "loading_bar1", 40, true);
            ui.loading_bar2 = Bar(loading, "loading_bar2", -40, false);

            ui.lowerRightPanel = ChamferGroup(root, "LowerRightPanel", r => {
                r.anchorMin = r.anchorMax = new Vector2(1, 0);
                r.pivot = new Vector2(1, 0);
                r.sizeDelta = new Vector2(360, 90);
                r.anchoredPosition = new Vector2(-40, 40);
            }, new Color(0.04f, 0.045f, 0.055f, 0.75f), 1f, chamferSmall);
            Label(ui.lowerRightPanel.gameObject, "Notice", "Placeholder build - see SETUP.md",
                18, new Color(1, 1, 1, 0.55f), Vector2.zero);

            SavePrefab(root, "LoginUI");
        }

        /// <summary>
        /// LoginUI.cs 对进度条调用 GetComponentInChildren&lt;Text&gt;(), 所以必须有子Text
        /// LoginUI calls GetComponentInChildren&lt;Text&gt;() on each bar, so a child Text is required.
        /// The anchors are what the tween drives, so leave them spanning.
        /// </summary>
        private static RectTransform Bar(GameObject parent, string name, float y, bool leftToRight) {
            GameObject track = Rect(parent, name + "_track", r => {
                r.anchorMin = new Vector2(0.5f, 0.5f);
                r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(900, 26);
                r.anchoredPosition = new Vector2(0, y);
            });
            Image trackImg = track.AddComponent<Image>();
            trackImg.color = new Color(1, 1, 1, 0.12f);

            GameObject fill = new GameObject(name, typeof(RectTransform));
            fill.transform.SetParent(track.transform, false);
            RectTransform fr = (RectTransform)fill.transform;
            fr.anchorMin = leftToRight ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            fr.anchorMax = leftToRight ? new Vector2(0.02f, 1f) : new Vector2(1f, 1f);
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = Accent;

            GameObject label = Rect(fill, name + "_text", r => {
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(-140, 0);
                r.offsetMax = new Vector2(140, 0);
            });
            Text t = label.AddComponent<Text>();
            Style(t, 20, Color.white);
            t.alignment = leftToRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            t.text = "0%";

            return fr;
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

            GameObject box = FramePanel(root, "Box", Centered(760, 300), Panel, 1f, true, out CanvasGroup _);

            // 两个背景层必须和Box一样是切角的, 否则方角会戳出橙色边框外
            // Both ground layers reuse the chamfer sprite: a square fill would poke out past
            // the framed panel's cut corners.
            ui.blackGround = ChamferFill(box, "blackGround", Stretch, new Color(0.05f, 0.05f, 0.06f, 1f));
            ui.whiteGround = ChamferFill(box, "whiteGround", Stretch, new Color(0.93f, 0.93f, 0.95f, 1f));
            ui.blackGround.SetActive(false);
            ui.whiteGround.SetActive(false);

            GameObject msg = Rect(box, "message", r => {
                r.anchorMin = new Vector2(0, 0.35f);
                r.anchorMax = new Vector2(1, 1f);
                r.offsetMin = new Vector2(40, 0);
                r.offsetMax = new Vector2(-40, -20);
            });
            Text msgText = msg.AddComponent<Text>();
            Style(msgText, 26, Color.white);
            msgText.supportRichText = true;
            ui.message = msgText;

            GameObject one = Rect(box, "mode_One", Stretch);
            ui.mode_One = one;
            ui.mode_One_back = Btn(one, "mode_One_back", "确 定", new Vector2(0, -100));

            GameObject two = Rect(box, "mode_Two", Stretch);
            ui.mode_Two = two;
            ui.mode_Two_enter = Btn(two, "mode_Two_enter", "确 定", new Vector2(-130, -100));
            ui.mode_Two_back = Btn(two, "mode_Two_back", "取 消", new Vector2(130, -100));
            two.SetActive(false);

            SavePrefab(root, "CommonDialogUI");
        }

        // ------------------------------------------------------------------ HomeUI (Lua driven)

        /// <summary>
        /// HomeUI 由 Lua 驱动, injections 的名字必须和 HomeUI.lua.txt 里的全局变量对应
        /// HomeUI is Lua driven: the injection names must match the globals HomeUI.lua.txt reads.
        /// </summary>
        private static void BuildHomeUIPrefab() {
            GameObject root = NewUIRoot("HomeUI");
            RuaUI ui = root.AddComponent<RuaUI>();

            GameObject bg = Rect(root, "BackGround", Stretch);
            bg.AddComponent<Image>().color = Ink;

            // move1 / move2 由 Lua 做视差 / Lua parallaxes these two transforms
            GameObject move1 = ChamferFill(root, "move1", Centered(1200, 700), new Color(0.20f, 0.23f, 0.28f, 0.7f));
            GameObject move2 = ChamferFill(root, "move2", Centered(900, 520), new Color(0.26f, 0.30f, 0.36f, 0.7f));

            // 顶栏是通栏的, 所以不切角 - 切角只用在四边都留空的浮动面板上
            // The HUD spans edge to edge, so it stays square: the chamfer is only for panels
            // that float clear of the screen edges.
            GameObject hud = GroupGo(root, "HUD", r => {
                r.anchorMin = new Vector2(0, 1);
                r.anchorMax = new Vector2(1, 1);
                r.pivot = new Vector2(0.5f, 1);
                r.sizeDelta = new Vector2(0, 150);
                r.anchoredPosition = Vector2.zero;
            }, new Color(0.04f, 0.045f, 0.055f, 0.85f), 1f);
            GameObject hudLine = Rect(hud, "AccentLine", r => {
                r.anchorMin = new Vector2(0, 0);
                r.anchorMax = new Vector2(1, 0);
                r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = new Vector2(0, 2f);
                r.anchoredPosition = Vector2.zero;
            });
            hudLine.AddComponent<Image>().color = Accent;

            Text clock = Label(hud, "time", "----/--/-- --:--", 26, Color.white, new Vector2(-700, -40));
            // 名字/等级/经验条要留开: Label是以父物体中心为锚, 经验条是以顶部为锚, 挤在一起会重叠
            // Name, level and the exp bar have to be spaced deliberately: Label anchors to the
            // parent's centre while the bar anchors to its top, so equal-looking offsets collide.
            Text pName = Label(hud, "playerName", "Doctor", 30, Color.white, new Vector2(-380, 15));
            Text pLevel = Label(hud, "playerLevel", "1", 24, Accent, new Vector2(-380, -20));
            Text reason = Label(hud, "reason", "0", 24, Color.white, new Vector2(80, -40));
            Text maxReason = Label(hud, "maxReason", "0", 24, new Color(1, 1, 1, 0.6f), new Vector2(180, -40));
            Text stone = Label(hud, "sourceStone", "0", 24, Color.white, new Vector2(380, -40));
            Text jade = Label(hud, "syntheticJade", "0", 24, Color.white, new Vector2(560, -40));
            Text coin = Label(hud, "dragonCoin", "0", 24, Color.white, new Vector2(740, -40));

            // 纯静态说明文字, 不进injections, Lua不认识它们 / static captions only, never injected
            Label(hud, "reasonCaption", "理智 SANITY", 15, TextDim, new Vector2(130, -8));
            Label(hud, "reasonSlash", "/", 24, TextDim, new Vector2(130, -40));
            Label(hud, "stoneCaption", "源石 ORIGINITE", 15, TextDim, new Vector2(380, -8));
            Label(hud, "jadeCaption", "合成玉 JADE", 15, TextDim, new Vector2(560, -8));
            Label(hud, "coinCaption", "龙门币 LMD", 15, TextDim, new Vector2(740, -8));

            GameObject expTrack = Rect(hud, "expTrack", r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(260, 10);
                r.anchoredPosition = new Vector2(-380, -112);
            });
            expTrack.AddComponent<Image>().color = new Color(1, 1, 1, 0.10f);

            GameObject expGo = Rect(hud, "expPercentage", r => {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(260, 10);
                r.anchoredPosition = new Vector2(-380, -112);
            });
            Image exp = expGo.AddComponent<Image>();
            exp.color = Accent;
            exp.sprite = Builtin("UISprite");
            exp.type = Image.Type.Filled;           // fillAmount 只有 Filled 才有视觉效果
            exp.fillMethod = Image.FillMethod.Horizontal;

            Label(root, "Hint", "Lua-driven screen. Move the mouse - parallax and clock come from HomeUI.lua.txt",
                20, new Color(1, 1, 1, 0.5f), new Vector2(0, -430));

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
            importer.spriteBorder = new Vector4(corner, corner, corner, corner);
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
            string path = UIPrefabDir + "/" + name + ".prefab";
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
