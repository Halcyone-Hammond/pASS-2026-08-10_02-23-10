using TMPro;
using UnityEngine;
using UnityEngine.Events;      // UnityAction: 按钮回调的委托类型
using UnityEngine.EventSystems; // EventSystem: 清按钮焦点用 (防止 Space 键重复触发按钮)
using UnityEngine.UI;          // 所有 UI 组件: Text / Button / Image / LayoutGroup ...
using static UnityEngine.Rendering.DebugUI.Table;

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
    Board board;           // 棋盘引用 (结算用)
    CameraSwitcher camSwitch; // 相机切换器引用 (视角按钮用)

    const int MaxValue = 10; // 数值上限 (原型默认值, 防止点出离谱数字)

    Piece current;    // 当前面板正在显示的棋子 (null = 没有选中)
    int energy = 10;  // 玩家能量 (原型默认 10; 每次 +/- 消耗 1)

    // ---- UI 引用 (BuildUI 里创建并保存, 后续刷新/点击时用) ----
    public GameObject panel;                    // 右侧面板本体 (隐藏/显示它)
    [SerializeField] TMP_Text hintText, reqText, titleText, resultText, energyText; // 提示条 / 目标 / 标题 / 结算结果 / 能量
    [SerializeField] Button minusBtn;  // [-] 按钮 (能量不足时置灰)
    [SerializeField] Button plusBtn;   // [+] 按钮
    Button whatPiece;

    // Unity 生命周期: Awake 最先执行, 这里把整套 UI 搭出来
    void Awake()
    {
        EnsureCanvasAndEventSystem(); // 兜底: 确保有 Canvas 和 EventSystem
    }

    // Start 在 Awake 之后执行: 补齐引用、默认收起面板、刷新能量显示
    void Start()
    {
        // 注意: FindObjectOfType 是旧 API, Unity 6 里已标记过时 (会有警告但能用);
        // 新 API 是 FindAnyObjectByType (见 Piece.cs)。这里保持原样未改。
        if (board == null) board = FindAnyObjectByType<Board>();
        if (camSwitch == null) camSwitch = FindAnyObjectByType<CameraSwitcher>();
        Hide();
        RefreshEnergyText();
        RefreshTargetText();
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

    // ---- 交互回调 ---------------------------------------------------------------

    // 正在修改成实时预览按钮进行修改。
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
        if (board == null) board = FindAnyObjectByType<Board>();
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
        if (camSwitch == null) camSwitch = FindAnyObjectByType<CameraSwitcher>();
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

    // 将矩阵Slot刷新，显示当前棋子
    void RefreshSlotButtons()
    {
        if (current == null) return;
        Vector2Int[] offs = current.GetOffsets(current.rot);
        for(int i = 0; i < offs.Length; i++)
        {
            Vector2Int pos = offs[i];
            // 通过棋盘坐标找到对应的格子物体, 再找它的 MatrixSelect 脚本
            Button thebtn = panel.gameObject.GetComponent<MatrixSelect>().buttonGuide(pos.x, pos.y);
            thebtn.gameObject.SetActive(true);
            thebtn.GetComponentInChildren<TMP_Text>().text = current.ValueOf(i).ToString();
            thebtn.GetComponent<Button>().image.color = current.GetComponent<Piece>().matTints[i].color;
        }
        
    }

    // 数值文字 + 能量不足时把 +/- 按钮置灰 (interactable = false)
    void RefreshValueTexts()
    {
        if (current == null) return;
        for (int l = 0; l < 4; l++)
        {
            //if (valueTexts[l] != null) valueTexts[l].text = current.ValueOf(l).ToString();
            if (minusBtn != null) minusBtn.interactable = energy > 0;
            if (plusBtn != null) plusBtn.interactable = energy > 0;
        }
    }

    // 能量文字 (面板内 + 顶部提示条) 同步
    void RefreshEnergyText()
    {
        if (energyText != null) energyText.text = "剩余能量: " + energy;
        if (hintText != null) hintText.text = "点击棋子拖动 | R 旋转 | Space 切换视角 | 能量: " + energy;
    }

    //目标为在能量文字下显示目标文字。显示“目标: 通路 左缘行X → 右缘行Y, S ≥ Z”。”尚未完成。
    void RefreshTargetText()
    {

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
        if (panel != null) panel.SetActive(false);
        if (current == null) return;
        Vector2Int[] offs = current.GetOffsets(current.rot);
        foreach (Vector2Int pos in offs)
        {
            // 通过棋盘坐标找到对应的格子物体, 再找它的 MatrixSelect 脚本
            Button thebtn = panel.gameObject.GetComponent<MatrixSelect>().buttonGuide(pos.x, pos.y);
            thebtn.gameObject.SetActive(true);
            thebtn.GetComponentInChildren<TMP_Text>().text = "0";
            thebtn.GetComponent<Button>().image.color = Color.lightGray;
        }
        current = null;
    }
}
