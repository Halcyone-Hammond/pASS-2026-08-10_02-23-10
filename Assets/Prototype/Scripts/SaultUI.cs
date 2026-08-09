using UnityEngine;
using UnityEngine.Events;      // UnityAction: 按钮回调的委托类型
using UnityEngine.EventSystems; // EventSystem: 清按钮焦点用 (防止 Space 键重复触发按钮)
using UnityEngine.UI;          // 所有 UI 组件: Text / Button / Image / LayoutGroup ...

// ============================================================
// SaultUI —— S.A.U.L. 配置面板 (右侧 UI)
// ============================================================
// 功能 (需求规定):
//   * 选中棋子后显示面板, 可重新分配 S/A/U/L 到 4 个槽位
//     (点击槽位字母按钮 -> 轮换到下一个字母, 若该字母已被别的槽位占用则自动交换,
//      因此永远保持"恰好一个 S、一个 A、一个 U、一个 L")
//   * 每个字母有自己的数值, 提供 +/- 按钮; 每次 +1 或 -1 消耗 1 点能量
//   * "提交结算"按钮: 让 Board 计算通路上的 S 总和, 显示 PASS / FAIL 和数值
// 面板结构 (全部用代码在运行时创建, 不需要手动搭 UI):
//   Canvas (ScreenSpaceOverlay)
//    ├─ 顶部提示条 (操作说明 + 能量)
//    └─ 右侧 Panel (VerticalLayoutGroup 自动纵向排列)
//        ├─ 标题 "棋子: X (SAUL 随槽位)"
//        ├─ 4 行: 槽位1~4 的字母按钮
//        ├─ 4 行: S/A/U/L 的 [-] [数值] [+] 
//        ├─ 能量显示
//        ├─ 目标说明文字
//        ├─ [提交结算] 按钮 + 结果文字
//        ├─ [切换视角 (Space)] 按钮
//        └─ [收起面板] 按钮
// ============================================================
public class SaultUI : MonoBehaviour
{
    public Board board;           // 棋盘引用 (结算用)
    public CameraSwitcher camSwitch; // 相机切换器引用 (视角按钮用)

    const int MaxValue = 9; // 数值上限 (原型默认值, 防止点出离谱数字)

    Piece current;    // 当前面板正在显示的棋子 (null = 没有选中)
    int energy = 10;  // 玩家能量 (原型默认 10; 每次 +/- 消耗 1)

    // ---- UI 引用 (BuildUI 里创建并保存, 后续刷新/点击时用) ----
    GameObject panel;                    // 右侧面板本体 (隐藏/显示它)
    Text hintText, titleText, resultText, energyText; // 提示条 / 标题 / 结算结果 / 能量
    Text[] slotTexts = new Text[4];      // 4 个槽位按钮上的字母文字
    Text[] valueTexts = new Text[4];     // 4 个字母的数值文字
    Button[] minusBtns = new Button[4];  // 4 个 [-] 按钮 (能量不足时置灰)
    Button[] plusBtns = new Button[4];   // 4 个 [+] 按钮

    // Unity 生命周期: Awake 最先执行, 这里把整套 UI 搭出来
    void Awake()
    {
        EnsureCanvasAndEventSystem(); // 兜底: 确保有 Canvas 和 EventSystem
        BuildUI();                    // 用代码创建全部 UI 元素
    }

    // Start 在 Awake 之后执行: 补齐引用、默认收起面板、刷新能量显示
    void Start()
    {
        // 注意: FindObjectOfType 是旧 API, Unity 6 里已标记过时 (会有警告但能用);
        // 新 API 是 FindAnyObjectByType (见 Piece.cs)。这里保持原样未改。
        if (board == null) board = FindObjectOfType<Board>();
        if (camSwitch == null) camSwitch = FindObjectOfType<CameraSwitcher>();
        Hide();
        RefreshEnergyText();
    }

    // 兜底: 正常情况下 Canvas 和 EventSystem 由 SceneBuilder 在编辑模式建好并保存进场景;
    // 这里再检查一次, 万一场景里没有也能在运行时补上, 保证 UI 可用。
    void EnsureCanvasAndEventSystem()
    {
        Canvas cv = GetComponent<Canvas>(); // SaultUI 本来就挂在 Canvas 物体上
        if (cv == null)
        {
            // 没有 Canvas -> 现场创建一个
            GameObject go = new GameObject("Canvas");
            go.transform.SetParent(transform, false);
            cv = go.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay; // 屏幕空间覆盖: UI 浮在画面最上层
            go.AddComponent<CanvasScaler>();    // 分辨率适配 (UI 按比例缩放)
            go.AddComponent<GraphicRaycaster>(); // 让 UI 能接收鼠标点击
        }
        // EventSystem 是 UI 事件的总调度器, 没有它按钮点不动
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // 本项目用"新输入系统", 所以事件模块必须用 InputSystemUIInputModule
            // (旧项目用的是 StandaloneInputModule, 在新输入系统下会失效)
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    // ---- 搭建 UI (全部代码生成) ------------------------------------------------
    void BuildUI()
    {
        // 顶部提示条: 固定贴在屏幕左上角 (anchor 左上角, 用 offset 控制边距)
        hintText = MakeText(transform, "", 14, new Color(0.12f, 0.13f, 0.16f), TextAnchor.MiddleLeft);
        RectTransform hr = hintText.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f);   // 锚点: 父物体的左上角
        hr.anchorMax = new Vector2(1f, 1f);   // 横向拉满整行
        hr.pivot = new Vector2(0f, 1f);       // 枢轴在左上角 (定位时以左上角为基准)
        hr.offsetMin = new Vector2(14f, -36f); // 距左 14、距下 36 (顶部一条横带)
        hr.offsetMax = new Vector2(-14f, -8f); // 距右 14、距上 8
        hintText.horizontalOverflow = HorizontalWrapMode.Overflow; // 文字不换行

        // 右侧面板: 锚定在屏幕右边缘中央
        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)); // Image 当背景
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.SetParent(transform, false);
        pr.anchorMin = new Vector2(1f, 0.5f); // 锚点: 父物体的右边缘中央
        pr.anchorMax = new Vector2(1f, 0.5f);
        pr.pivot = new Vector2(1f, 0.5f);     // 枢轴在面板右边缘中央
        pr.anchoredPosition = new Vector2(-12f, 0f); // 距屏幕右缘 12 像素
        pr.sizeDelta = new Vector2(250f, 570f);      // 宽 250、高 570
        panel.GetComponent<Image>().color = new Color(0.96f, 0.96f, 0.99f, 0.97f); // 近白底

        // 面板内的"内容区": 用 VerticalLayoutGroup 让所有行自动从上往下排列,
        // 不用手动算每个控件的位置 (这是 Unity UI 布局系统最省事的用法)
        RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(panel.transform, false);
        content.anchorMin = Vector2.zero; // 锚点铺满整个面板
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(8f, 8f);  // 四周留 8 像素内边距
        content.offsetMax = new Vector2(-8f, -8f);
        VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 5f;                 // 行间距 5
        vlg.childControlWidth = true;     // 子物体宽度由布局组控制 (拉满)
        vlg.childControlHeight = true;    // 子物体高度由布局组控制
        vlg.childForceExpandWidth = true; // 强制子物体撑满宽度

        // 标题行
        titleText = MakeText(content, "", 16, new Color(0.1f, 0.12f, 0.16f), TextAnchor.MiddleLeft);

        // 4 个槽位行: 标签 + 字母按钮。
        // 字母按钮点击 -> OnSlotCycle(slot): 轮换字母并自动与冲突槽位交换。
        for (int i = 0; i < 4; i++)
        {
            int slot = i; // 先复制到局部变量! 闭包陷阱: 循环变量 i 在回调触发时已变成 4,
                          // 所以必须用局部变量 slot 才能在回调里拿到"当前这一次的 i"
            HorizontalLayoutGroup row = MakeRow(content); // 这一行是横向排列
            Text lab = MakeText(row.transform, "槽位" + (i + 1), 14, new Color(0.15f, 0.16f, 0.2f), TextAnchor.MiddleLeft);
            LayoutElement lle = lab.gameObject.AddComponent<LayoutElement>(); // 指定最小宽度, 防止被挤压
            lle.minWidth = 62;
            Button b = MakeButton(row.transform, "S", 14, delegate { OnSlotCycle(slot); });
            LayoutElement ble = b.gameObject.AddComponent<LayoutElement>();
            ble.minWidth = 96; ble.minHeight = 26; // 按钮最小尺寸
            slotTexts[i] = b.GetComponentInChildren<Text>(); // 记住按钮里的文字 (刷新用)
        }

        // 4 个属性数值行: [字母] [-] [数值] [+]
        for (int l = 0; l < 4; l++)
        {
            int letter = l; // 同样复制到局部变量, 避免闭包陷阱
            HorizontalLayoutGroup row = MakeRow(content);
            Text lab = MakeText(row.transform, "SAUL"[l].ToString(), 15, new Color(0.1f, 0.12f, 0.16f), TextAnchor.MiddleCenter);
            LayoutElement lle = lab.gameObject.AddComponent<LayoutElement>();
            lle.minWidth = 26;
            Button mb = MakeButton(row.transform, "−", 15, delegate { OnValue(letter, -1); }); // 减 1
            LayoutElement mle = mb.gameObject.AddComponent<LayoutElement>();
            mle.minWidth = 40; mle.minHeight = 26;
            minusBtns[l] = mb;
            valueTexts[l] = MakeText(row.transform, "1", 15, new Color(0.1f, 0.12f, 0.16f), TextAnchor.MiddleCenter);
            LayoutElement vle = valueTexts[l].gameObject.AddComponent<LayoutElement>();
            vle.minWidth = 44;
            Button pb = MakeButton(row.transform, "+", 15, delegate { OnValue(letter, 1); }); // 加 1
            LayoutElement ple = pb.gameObject.AddComponent<LayoutElement>();
            ple.minWidth = 40; ple.minHeight = 26;
            plusBtns[l] = pb;
        }

        // 能量显示
        energyText = MakeText(content, "", 14, new Color(0.1f, 0.12f, 0.16f), TextAnchor.MiddleLeft);

        // 目标说明 (常量来自 Board, 改 Board 常量这里自动跟着变)
        Text req = MakeText(content, "目标: 通路 左缘行" + Board.EntryRow + " → 右缘行" + Board.ExitRow + ", S ≥ " + Board.TargetS,
            13, new Color(0.35f, 0.36f, 0.42f), TextAnchor.MiddleLeft);
        req.horizontalOverflow = HorizontalWrapMode.Wrap; // 允许换行
        LayoutElement rle = req.gameObject.AddComponent<LayoutElement>();
        rle.minHeight = 40; // 两行文字的高度

        // 提交结算按钮
        Button submit = MakeButton(content, "提交结算 (Submit)", 14, OnSubmit);
        LayoutElement sle = submit.gameObject.AddComponent<LayoutElement>();
        sle.minHeight = 32;

        // 结算结果文字 (PASS / FAIL / 未提交)
        resultText = MakeText(content, "未提交", 15, new Color(0.2f, 0.2f, 0.24f), TextAnchor.MiddleCenter);
        LayoutElement cl = resultText.gameObject.AddComponent<LayoutElement>();
        cl.minHeight = 30;

        // 视角切换按钮 (效果等同按 Space)
        Button camBtn = MakeButton(content, "切换视角 (Space)", 13, delegate { ToggleCamera(); ClearFocus(); });
        LayoutElement cble = camBtn.gameObject.AddComponent<LayoutElement>();
        cble.minHeight = 28;

        // 收起面板按钮
        Button close = MakeButton(content, "收起面板", 13, delegate { Hide(); ClearFocus(); });
        LayoutElement xle = close.gameObject.AddComponent<LayoutElement>();
        xle.minHeight = 28;

        RefreshEnergyText();
    }

    // 生成一个"横向一行"的容器: HorizontalLayoutGroup 让子物体从左往右排
    HorizontalLayoutGroup MakeRow(Transform parent)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 6f;                 // 子物体间距 6
        h.childControlWidth = true;     // 子物体宽度由布局组控制
        h.childControlHeight = true;    // 子物体高度由布局组控制
        h.childForceExpandWidth = false; // 不强制撑满 (按各自的最小宽度排)
        h.childForceExpandHeight = true;  // 高度撑满这一行
        return h;
    }

    // 生成一个按钮: 底图 (Image) + 交互 (Button) + 文字 (Text)
    // UnityAction 是 Unity 的"无参数回调"委托, 用来接点击事件
    Button MakeButton(Transform parent, string label, int size, UnityAction onClick)
    {
        GameObject go = new GameObject("Btn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();                       // 背景图片 (纯色矩形)
        img.color = new Color(0.24f, 0.35f, 0.55f);                 // 深蓝底色
        Button b = go.AddComponent<Button>();                       // 点击交互组件
        b.targetGraphic = img;                                      // 按钮高亮/变灰效果作用在底图上
        b.onClick.AddListener(onClick);                             // 注册点击回调
        Text t = MakeText(go.transform, label, size, Color.white, TextAnchor.MiddleCenter);
        RectTransform tr = t.rectTransform;
        tr.anchorMin = Vector2.zero; // 文字铺满整个按钮
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        return b;
    }

    // 生成一个 Text 文字 (Unity UI 的 Text, 不是 3D 的 TextMesh)
    Text MakeText(Transform parent, string s, int size, Color color, TextAnchor align)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = LoadFont();        // 必须手动赋字体, 否则不显示
        t.text = s;
        t.fontSize = size;          // 字号
        t.color = color;            // 颜色
        t.alignment = align;        // 对齐方式 (TextAnchor 枚举)
        t.horizontalOverflow = HorizontalWrapMode.Overflow; // 超出不换行 (默认)
        t.verticalOverflow = VerticalWrapMode.Overflow;     // 超出不裁剪
        t.raycastTarget = false;    // 重要: 文字不参与射线检测, 否则会挡住按钮点击
        return t;
    }

    // 加载内置字体 (UI 文字专用; 与 Piece 里的 3D 字体加载同理, 兼容新旧 Unity)
    static Font cachedFont;
    static Font LoadFont()
    {
        if (cachedFont != null) return cachedFont;
        try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (System.Exception) { }
        if (cachedFont == null) { try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (System.Exception) { } }
        return cachedFont;
    }

    // ---- 交互回调 ---------------------------------------------------------------

    // 点击槽位字母按钮: 轮换到下一个字母。
    // 规则: 下一个字母若已被别的槽位使用, 就和那个槽位"交换字母",
    // 从而始终保持"每个字母恰好一个" (这是需求硬性约束)。
    // 例: 槽位1=S 槽位2=A, 点槽位1 -> 下一个字母是 A, 被槽位2占用 ->
    //     交换后 槽位1=A, 槽位2=S。
    void OnSlotCycle(int slot)
    {
        if (current == null) return;
        int nl = (current.slotLetters[slot] + 1) % 4; // 下一个字母 (0..3 循环)
        for (int j = 0; j < 4; j++)
        {
            if (j != slot && current.slotLetters[j] == nl)
            {
                current.slotLetters[j] = current.slotLetters[slot]; // 冲突槽位拿走旧字母
                break;
            }
        }
        current.slotLetters[slot] = nl; // 本槽位换上字母 nl
        current.RefreshVisuals();  // 棋子视觉同步 (顶面颜色 + 标签文字)
        RefreshSlotButtons();      // 面板按钮文字同步
        ClearFocus();              // 清按钮焦点, 防止 Space 键误触发
    }

    // 点击 +/- 按钮: 修改某字母的数值, 消耗 1 点能量
    void OnValue(int letter, int delta)
    {
        if (current == null || energy < 1) return; // 没选棋子 / 没能量 -> 不响应
        int nv = Mathf.Clamp(current.ValueOf(letter) + delta, 0, MaxValue); // 限制在 0~9
        if (nv == current.ValueOf(letter)) return; // 已在边界 (0 或 9), 不消耗能量
        current.SetValue(letter, nv);
        energy--; // 每次 +1/-1 消耗 1 能量
        RefreshAll();
        ClearFocus();
    }

    // 点击提交结算: 让 Board 算通路和 S 总和, 显示 PASS / FAIL
    void OnSubmit()
    {
        if (board == null) board = FindObjectOfType<Board>();
        if (board == null) { resultText.text = "无棋盘"; return; }
        bool pass; int sum; string msg;
        board.Submit(out pass, out sum, out msg); // out 参数带回三个结果
        resultText.text = msg; // 如 "PASS — S=6 ≥ 4"
        resultText.color = pass ? new Color(0.08f, 0.55f, 0.2f) : new Color(0.8f, 0.18f, 0.18f); // 绿/红
        ClearFocus();
    }

    // 视角切换按钮: 调 CameraSwitcher 的 Toggle (和按 Space 一样)
    void ToggleCamera()
    {
        if (camSwitch == null) camSwitch = FindObjectOfType<CameraSwitcher>();
        if (camSwitch != null) camSwitch.Toggle();
        ClearFocus();
    }

    // 清掉 EventSystem 当前选中的 UI 对象。
    // 原因: 如果按钮还"持有焦点", 之后按 Space/Enter 会再次触发它 (比如提交两次),
    // 所以每次点完按钮都把焦点清掉, 让 Space 只负责切换相机。
    void ClearFocus()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // ---- 刷新显示 ---------------------------------------------------------------

    void RefreshAll()
    {
        RefreshSlotButtons();
        RefreshValueTexts();
        RefreshEnergyText();
    }

    // 槽位按钮上的字母文字 与 棋子的实际分配 保持一致
    void RefreshSlotButtons()
    {
        if (current == null) return;
        for (int i = 0; i < 4; i++)
            if (slotTexts[i] != null) slotTexts[i].text = "SAUL"[current.slotLetters[i]].ToString();
    }

    // 数值文字 + 能量不足时把 +/- 按钮置灰 (interactable = false)
    void RefreshValueTexts()
    {
        if (current == null) return;
        for (int l = 0; l < 4; l++)
        {
            if (valueTexts[l] != null) valueTexts[l].text = current.ValueOf(l).ToString();
            if (minusBtns[l] != null) minusBtns[l].interactable = energy > 0;
            if (plusBtns[l] != null) plusBtns[l].interactable = energy > 0;
        }
    }

    // 能量文字 (面板内 + 顶部提示条) 同步
    void RefreshEnergyText()
    {
        if (energyText != null) energyText.text = "能量 (Energy): " + energy;
        if (hintText != null) hintText.text = "点击棋子拖动 | R 旋转 | Space 切换视角 | 能量: " + energy;
    }

    // 显示面板并指向某棋子 (由 InputController 选中棋子时调用)
    public void ShowFor(Piece p)
    {
        current = p;
        panel.SetActive(true);
        titleText.text = "棋子: " + p.shapeName + "  (SAUL 随槽位)";
        resultText.text = "未提交";
        resultText.color = new Color(0.2f, 0.2f, 0.24f);
        RefreshAll();
    }

    // 收起面板 (由 InputController 取消选中 / 点击"收起面板"按钮时调用)
    public void Hide()
    {
        current = null;
        if (panel != null) panel.SetActive(false);
    }
}
