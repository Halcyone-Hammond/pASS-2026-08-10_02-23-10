using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems; // EventSystem: 用来判断鼠标是否点在 UI 上
using UnityEngine.InputSystem;  // 新版输入系统 (本项目用的是新输入系统, 不能用旧的 UnityEngine.Input)

// ============================================================
// InputController —— 鼠标交互 (输入层)
// ============================================================
// 完整流程 (需求规定):
//   鼠标点击/拖动 -> Physics.Raycast 发射射线 -> 得到世界坐标落点
//   -> 换算成棋盘格子坐标 -> 操作棋盘数组 (Board)
// 能力:
//   * 点击选中棋子 (棋盘上的 或 托盘里未放置的)
//   * 按住拖动, 棋子跟随鼠标平滑移动
//   * 松手吸附到最近格子; 越界或与其他棋子重叠 -> 拒绝并回退 (原位或托盘)
//   * R 键旋转选中棋子 90°
// 注意: 所有规则判定都走 Board 数组, 不用物理碰撞做规则。
// ============================================================
public class InputController : MonoBehaviour
{
    [SerializeField] Board board;   // 棋盘引用 (SceneBuilder 搭建场景时赋值)
    [SerializeField] SaultUI ui;    // 右侧配置面板引用 (选中棋子后要显示它)
    [SerializeField] StateManager stateManager;


    // ---- 拖拽状态 ---------------------------------------------------------------
    bool oriPlaced;        // 拖拽开始时棋子是否已放置 (true = 原来在棋盘上, false = 托盘里)
    int origX, origZ, origRot; // 拖拽开始时棋子在棋盘上的格子坐标和旋转角度 (放在托盘里时无效)
    Piece selected, hovered;       // 当前选中的棋子 (null = 没选中)
    bool dragging;        // 是否正在拖拽
    Vector3 mouseOffset, grabOffset=Vector3.zero;   // 鼠标按下点到棋子锚点的偏移 (保证"抓着棋子哪, 哪里就跟着鼠标")


    // Unity 生命周期: Update 每帧调用一次 (与帧率相关, 60fps 就是每秒 60 次)。
    // 输入系统的"按下/松开"是按帧判断的: wasPressedThisFrame = "这一帧刚按下"。
    private void Awake()
    {
        selected = null; hovered = null;
    }

    void Update()
    {
        // Mouse.current 是 Input System 提供的鼠标设备; 没有鼠标设备时为 null (要判空)
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Piece hoverDetact;
            Vector2 mousePos = mouse.position.ReadValue();
            hoverDetact = HoverIndicate(mousePos);                                                                    //增加一个悬浮高亮的模块
            // 小状态机: 刚按下 -> 尝试选中; 按住中 -> 跟随拖动; 刚松开 -> 放置
            if (mouse.leftButton.wasPressedThisFrame)
            {
                grabOffset = mouseOffset;
                TrySelect(hoverDetact);
            }
            else if (mouse.leftButton.isPressed && dragging) DragUpdate(mousePos);
            else if (mouse.leftButton.wasReleasedThisFrame && dragging) DragEnd();
        }
        // 将采用按键旋转……
    }

    //鼠标悬浮高亮 
    Piece HoverIndicate(Vector2 screenPos)
    {
        if(hovered)  hovered.SetSelected(false);
        // 如果鼠标点在 UI 上 (按钮/面板), 就不要去选棋子, 否则点按钮也会误拖棋子
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;
        // 从相机位置穿过鼠标的屏幕坐标发射一条射线 (screenPos 是屏幕像素坐标)
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        RaycastHit hit; // 射线命中信息: 命中点位置、命中的碰撞体等
        if (!Physics.Raycast(ray, out hit, 1000)) return null; 
        Vector3 wp = hit.point; // 世界坐标落点 (在棋盘 collider 的表面上)
        // 先看落点在棋盘内且格子上有棋子 -> 就是它
        int x, z;
        Piece p = null;
        if (board != null && board.WorldToCell(wp, out x, out z) && board.board[x, z].piece != null)
            p = board.board[x, z].piece;
        // 否则在托盘区找离落点最近的未放置棋子
        if (p == null) p = FindNearbyTrayPiece(wp);
        if (p == null) {
            hovered = null;
            return null; }
        hovered = p;
        p.SetSelected(true);     // 高亮
        // 记录"抓取点": 鼠标点在哪就保持哪跟着鼠标, 而不是让棋子中心跳过去
        mouseOffset = p.transform.position - wp;
        mouseOffset.y = 0f;
        return p;
    }

    // 鼠标按下: 决定点到了什么
    void TrySelect(Piece thepiece)
    {
        // 如果鼠标点在 UI 上 (按钮/面板), 就不要去选棋子, 否则点按钮也会误拖棋子
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (thepiece == null) { Deselect(); return; } // 点的是空格 -> 取消选中
        oriPlaced = thepiece.placed;
        Select(thepiece);
    }

    // 在托盘区 (棋盘外) 按最近距离查找未放置的棋子。
    // 托盘里的棋子不在棋盘数组里, 只能按"世界坐标距离"找:
    // 遍历每个未放置棋子的 4 个槽位中心, 看哪个离鼠标落点最近 (0.8 格以内)。
    Piece FindNearbyTrayPiece(Vector3 wp)
    {
        Piece best = null;
        float bestD = 0.8f; // 判定半径: 约一个格子大小
        if (board == null) return null;
        for (int i = 0; i < board.allPieces.Count; i++)
        {
            Piece p = board.allPieces[i];
            if (p == null || p.placed) continue; // 只找未放置的
            Vector2Int[] offs = p.GetOffsets(0); // 槽位偏移 (格子单位)
            for (int j = 0; j < offs.Length; j++)
            {
                // 槽位的世界坐标 = 棋子位置 + 偏移 (棋盘坐标轴与 XZ 平面一致, 可直接相加)
                Vector3 cw = p.transform.position + new Vector3(offs[j].x, 0f, offs[j].y);
                // 只比较 XZ 平面上的距离 (y 方向无关紧要)
                float d = Vector2.Distance(new Vector2(cw.x, cw.z), new Vector2(wp.x, wp.z));
                if (d < bestD) { bestD = d; best = p; }
            }
        }
        return best;
    }

    // 开始选中并拖拽棋子
    void Select(Piece p)
    {
        Deselect(); // 先取消上一个选中 (清状态、收面板)
        selected = p;
        dragging = true;

        if(oriPlaced)
        {
            origX = p.bx; origZ = p.bz; origRot = p.rot; // 记录原来的格子坐标和旋转角度
            stateManager.Pending();
            // 关键: 拖起来的第一时间就从棋盘数组里"摘下来",
            // 这样拖拽过程中它不占格子, 重叠校验也不用考虑它自己
            if (board != null) board.RemovePiece(p);
            p.placed = false; // 拖起来的棋子不算放置 (否则拖拽中就会被判定为"已放置")
        }
        else
        {
            stateManager.Pending();
            //origRot = p.rot; // 托盘里没有格子坐标, 只记录旋转角度
        }
    }

    // 拖拽中: 每帧把棋子移向"鼠标世界位置 + 抓取偏移"
    void DragUpdate(Vector2 screenPos)
    {
        if (selected == null) return;
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        RaycastHit hit;
        Vector3 target = selected.transform.position; // 默认原地 (射线没命中就不动)
        if (Physics.Raycast(ray, out hit, 1000f))
        {
            target = hit.point + grabOffset; // 落点 + 抓取偏移
            target.y = 0f;                  // 贴地 (棋盘表面高度)
        }
        // Lerp 插值: 每帧向目标移动 45% -> 平滑跟随, 不跳变
        selected.transform.position = Vector3.Lerp(selected.transform.position, target, 0.45f);
        if(board!= null) ShowGhost(selected, target); // 直接移动预览
    }

    void ShowGhost(Piece pieceHover, Vector3 targetPos)
    {
        int x, z;

        if(!board.WorldToCell(targetPos, out x, out z))      //不在边界之中
        {
            board.GhostlyRecover();
            return;
        }
        board.GhostlyTrial(pieceHover, x, z, pieceHover.rot); // 预览放置 (内部会做越界/重叠校验)
    }

    // 松手: 尝试放到最近的格子
    void DragEnd()
    {
        if (selected == null)
        {
            stateManager.CancelPending();
            return;
        }
        int x, z;
        bool ok = false;
        board.GhostlyRecover();
        if (board != null)
        {
            bool boundIn = board.WorldToCell(selected.transform.position, out x, out z);
            // 棋子当前位置 -> 最近格子 -> 尝试放置 (内部会做越界/重叠校验)
            if (boundIn)
            {
                ok = board.TryPlace(selected, x, z, selected.rot);
                // 放不下且原来就在棋盘上 -> 放回原位
                if (!ok && oriPlaced)
                {
                    stateManager.CancelPending();
                    ok = board.TryPlace(selected, origX, origZ, origRot);
                }
                else if (!ok && !oriPlaced)
                {
                    stateManager.CancelPending();
                    selected.GoToTray();
                    ui.Hide();
                }
                else
                {
                    stateManager.PushTemp();
                    if (ui != null) ui.ShowFor(selected); // 弹出 SAUL 配置面板
                }
            }
            else
            {
                if(!oriPlaced)
                    stateManager.CancelPending();
                else
                    stateManager.PushTemp();
                selected.GoToTray(); // 都失败 (原来在托盘) -> 直接回托盘，无需改变board数组，因为它本来就不在里面
                ui.Hide();
            }
            dragging = false;
            grabOffset = Vector3.zero;
            if (!selected.placed) return;
            selected.SetSelected(true);   // 保持选中 (方便继续 R 旋转 / 改 SAUL)
            selected.placed = true; // 放置成功
        }
    }

    // R 键: 选中棋子旋转 90°
    void RotateSelected()
    {
        if (selected == null) return;
        int nr = (selected.rot + 1) % 4; // 0->1->2->3->0 循环
        if (selected.placed)
        {
            // 已在棋盘上: 原地换旋转角度; 新角度放不下 (越界/被挡) 则保持原角度
            // (TryPlace 失败时不改任何状态, 正好符合"保持原样")
            if (board != null) board.TryPlace(selected, selected.bx, selected.bz, nr);
        }
        else
        {
            // 拖拽中/托盘里: 只转视觉, 不做格子校验 (松手时才校验)
            selected.rot = nr;
            selected.SetVisualRotation(nr);
        }
    }

    // 取消选中: 取消高亮、清状态、收起面板
    public void Deselect()
    {
        if (selected != null) selected.SetSelected(false);
        selected = null;
        dragging = false;
        if (ui != null) ui.Hide();
    }
}
