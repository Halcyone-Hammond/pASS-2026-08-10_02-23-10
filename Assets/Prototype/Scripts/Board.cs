using System.Collections.Generic;   // List<T>、Queue<T> 等泛型容器所在的命名空间
using UnityEngine;                  // MonoBehaviour、Vector3、Material 等所有 Unity 核心类型

// ============================================================
// Board —— 棋盘: 整个原型的数据核心
// ============================================================
// 设计要点 (来自原型需求):
//   * 棋盘是 4x5 的格子阵列, 放在场景的 XZ 平面上 (俯视时看起来像 2D 棋盘)
//   * 用二维数组 BoardCell[5,4] 作为"唯一真相来源" (source of truth):
//     所有关于"哪个格子被哪个棋子的哪个槽位占据"的信息都只从数组读取
//   * 棋子移动/旋转/校验全部用数组计算, 完全不用物理 (刚体/碰撞) 来管规则
//   * 只有"鼠标点哪里"这一步用到 Physics.Raycast (射线), 那是输入手段, 不是规则
// ============================================================
public class Board : MonoBehaviour
{
    // ---- 棋盘尺寸与关卡常量 ---------------------------------------------------
    // 常量 (const) 在编译期就固定, 运行时不能改, 适合这类"写死的规则数字"
    public const int W = 7;              // 列数 (x 方向): 棋盘宽 7 格
    public const int H = 6;              // 行数 (z 方向): 棋盘高 6 格
    int boardStage = 1;          // 关卡阶段 (0~2), 控制棋盘大小, 初始为 2，再慢慢解锁到 1，0
    bool visualYet = false;

    int leftBorderX, rightBorderX;         // 入口所在列: 左边缘 (x = 0)// 出口所在列: 右边缘 (x = 6)
    int upperBorderZ, lowerBorderZ;        // 上边缘 (z = 5)
    float wCenter, hCenter;                       // 用于求出棋盘中心，实现对称发展
    bool[] entryX = new bool[7];
    bool[] exitX = new bool[7];
    bool[] entryZ = new bool[6];
    bool[] exitZ = new bool[6];

    //NPC老师的事件要够丰富够多……
    [SerializeField] public int TargetS = 4;        // 结算目标
    [SerializeField] public int PerkT = 1;          //提供什么呢？提供基于当前数值的Perk，倍增分数）——这个是非操作阶段内的事情，暂时不管
    public int decideLetter;
    int levelDifficulty = 0;
    int xyDecider;              // BFS出入方向默认抉择器，为0是横向，为1是纵向
    // 标记数量会随关卡难度变化，用 List 才能安全地动态追加。
    readonly List<GameObject> teacherMarkers = new List<GameObject>();


    public GameObject[] ghostBlocks = new GameObject[6]; // 用于显示预览的格子 (鼠标拖动棋子时显示)

    // ---- 格子数据 -------------------------------------------------------------
    // [System.Serializable] 让这个普通类可以被 Unity 序列化 (可显示在 Inspector、
    // 可随场景保存)。BoardCell 记录"这一个格子"的占用信息。
    [System.Serializable]
    public class BoardCell
    {
        public Piece piece;   // 占据这个格子的棋子; null = 空格
        public int slot = -1; // 该棋子的第几个槽位 (0~3) 占了这个格子; -1 = 无
    }

    // 核心数组: [x, z] 下标对应格子位置, 与设计文档里的 BoardCell[7,6] 一致。
    // 注意: 数组元素是引用类型, 必须先 new 出每个 BoardCell 才能用 (见 Awake)。
    public BoardCell[,] board = new BoardCell[W, H];
    // 场景里所有棋子的清单 (含已放置的和托盘里的), 方便遍历查找
    public List<Piece> allPieces = new List<Piece>();

    // 棋盘视觉用的材质 (由 SceneBuilder 在搭建场景时赋值;
    // 留了 null 兜底: BuildBoard 里会自动 new 一个, 防止忘赋导致粉红色)
    [SerializeField] public Material matTile, matEdge, matGround, matEntry, matExit, matGhost;

    StateManager stateManager;

    // 静态单例: 全局唯一的"当前棋盘"引用, 其他脚本可以直接 Board.Instance 访问
    public static Board Instance;

    Vector3 ini = new Vector3(100, 0.26f, 100);

    // ---- Unity 生命周期: Awake -------------------------------------------------
    // Awake 在场景加载、脚本被激活时最先执行一次 (早于 Start)。
    // 这里做两件事: 记录单例、给数组的每个元素 new 出 BoardCell。
    void Awake()
    {
        Instance = this;
        leftBorderX = boardStage;
        rightBorderX = W - boardStage;
        upperBorderZ = H - boardStage;
        lowerBorderZ = boardStage;
        wCenter = (rightBorderX + leftBorderX - 1) * 0.5f;
        hCenter = (upperBorderZ + lowerBorderZ - 1) * 0.5f;
        Debug.Log("Board Awake: leftBorderX=" + leftBorderX + ", rightBorderX=" + rightBorderX + ", upperBorderZ=" + upperBorderZ + ", lowerBorderZ=" + lowerBorderZ);

        BuildBoard();
       
        // 二维数组的每个元素都要实例化, 否则访问 board[x,z].piece 会空引用报错
        for (int x = 0; x < W; x++)
            for (int z = 0; z < H; z++)
                board[x, z] = new BoardCell();

        RandomTeacher();
    }

    public void ClearBoardCells()
    {
        for (int x = 0; x < W; x++)
            for (int z = 0; z < H; z++)
            {
                board[x, z].piece = null;
                board[x, z].slot = -1;
            }
        return;
    }

    private void Start()
    {
        for (int x = 0; x < 6; x++)
        {
            Vector3 iniPos = new Vector3(ini.x + x, ini.y, ini.z);
            ghostBlocks[x] = CommonPlace.Instance.Prim(x.ToString(), iniPos, new Vector3(1.05f, 1.16f, 1.05f), matGhost);
        }
        if (stateManager == null) stateManager = FindAnyObjectByType<StateManager>();
    }

    // ---- 随机化入口/出口/目标 ---------------------------------------------------
    public void RandomTeacher()
    {
        ClearTeacherMarkers();
        //目前的设计状态下，先确定EntryX,EntryZ,exitX,exitZ——以及对于数值的要求
        xyDecider = Random.Range(0, 2);                                 //默认只判定一个方向的流动，但是在游戏过程是可以多加的。
        if (levelDifficulty == 0)
        {
            if(xyDecider == 0)
            {
                for (int i = leftBorderX; i < rightBorderX; i++)
                { entryX[i] = true; exitX[i] = true; }
            }

            else
            {
                for (int i = lowerBorderZ; i < upperBorderZ; i++)
                { entryZ[i] = true; exitZ[i] = true; }
            }

        }
        else if (levelDifficulty == 1)
        {
            if(xyDecider == 0)
            {
                int entranceX = Random.Range(leftBorderX, rightBorderX - 1);
                entryX[entranceX] = true; entryX[entranceX + 1] = true;
                int wayoutX = Random.Range(leftBorderX, rightBorderX - 1);
                exitX[wayoutX] = true; exitX[wayoutX + 1] = true;
            }

            else
            {
                int entranceZ = Random.Range(lowerBorderZ, upperBorderZ - 1); 
                entryZ[entranceZ] = true; entryZ[entranceZ + 1] = true;
                int wayoutZ = Random.Range(lowerBorderZ,upperBorderZ - 1);
                exitZ[wayoutZ] = true; exitZ[wayoutZ + 1]=true;
            }    
        }

        else
        {
            if(xyDecider ==0)
            {
                int entranceX = Random.Range(leftBorderX, rightBorderX);
                entryX[entranceX] = true;
                int wayoutX = Random.Range(leftBorderX, rightBorderX);
                exitX[wayoutX] = true;
            }

            else
            {
                int entranceZ = Random.Range(lowerBorderZ, upperBorderZ);
                entryZ[entranceZ] = true;
                int wayoutZ = Random.Range(lowerBorderZ, upperBorderZ);
                exitZ[wayoutZ] = true;
            }
        }

        //老师讲清楚要求
        decideLetter = Random.Range(0, 4);
        TargetS = Random.Range(7, 27);

        // 入口 / 出口 标记 (绿 = 入口, 红 = 出口)
        if (matEntry != null)
        {
            for(int i = 0; i< entryX.Length; i++)
            {
                int index = i;
                if (entryX[index])
                    teacherMarkers.Add(makeEntryExitMarker(CellToWorld(index, upperBorderZ), true));
            }
            for(int i = 0; i< entryZ.Length; i++)
            {
                int index = i;
                if (entryZ[index])
                    teacherMarkers.Add(makeEntryExitMarker(CellToWorld(leftBorderX-1, index), true));
            }
        }
        if (matExit != null)
        {
            for(int i = 0; i < exitX.Length; i++)
            {
                int index = i;
                if (exitX[index])
                    teacherMarkers.Add(makeEntryExitMarker(CellToWorld(index, lowerBorderZ - 1), false));
            }
            for(int i =0; i<exitZ.Length; i++)
            {
                int index = i;
                if (exitZ[index])
                    teacherMarkers.Add(makeEntryExitMarker(CellToWorld(rightBorderX, index), false));
            }
        }

    }

    void ClearTeacherMarkers()
    {
        foreach (GameObject marker in teacherMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        teacherMarkers.Clear();

        // RandomTeacher 可能被重复调用，旧的入口/出口规则也必须一起清掉。
        System.Array.Clear(entryX, 0, entryX.Length);
        System.Array.Clear(exitX, 0, exitX.Length);
        System.Array.Clear(entryZ, 0, entryZ.Length);
        System.Array.Clear(exitZ, 0, exitZ.Length);
    }

    GameObject makeEntryExitMarker(Vector3 pos, bool entryExit)
    {
        GameObject go = CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(0.28f, 0.05f, 0.28f);
        go.transform.localPosition = pos + new Vector3(0f, 0.065f, 0f);
        if (entryExit)
        {
            go.name = "EntryMarker";
            SetMat(go, matEntry);
        }
        else
        {
            go.name = "ExitMarker";
            SetMat(go, matExit);
        }
        return go;
    }

    // 把棋子登记进 allPieces 清单 (用 Contains 防重复登记)
    public void RegisterPiece(Piece p)
    {
        if (!allPieces.Contains(p)) allPieces.Add(p);
    }

    // ---- 坐标系转换 -------------------------------------------------------------
    // 格子的中心在世界坐标哪里?
    // 棋盘以原点为中心
    // 格子尺寸 = 1 单位, 所以格子 (x,z) 的中心 = 相对于"有效区中心"的偏移:
    public Vector3 CellToWorld(int x, int z)
    {
        float xx = x - wCenter;
        float zz = z - hCenter;
        //Debug.Log("translating "+ x + "," + z + " to " + xx + "," + zz);
        return new Vector3(xx, 0f, zz);
    }

    // 反过来: 世界坐标 -> 最近的格子坐标。
    // 先把世界坐标平移回"以原点为中心"的坐标系, 再四舍五入取最近的整数格。
    // 返回值 (bool) 表示这个点是否落在棋盘范围内; 格子坐标通过 out 参数带回。
    // (C# 的 out 参数: 方法内必须给它赋值, 调用方用 out int x, out int z 接收)
    public bool WorldToCell(Vector3 p, out int x, out int z)
    {
        x = Mathf.RoundToInt(p.x + wCenter);                             // 平移回以原点为中心的坐标系, 再四舍五入取最近的整数格
        z = Mathf.RoundToInt(p.z + hCenter);                             // 本质为将点与格子中心对齐, 取最近的格子坐标
        return x >= leftBorderX && x < rightBorderX && z >= lowerBorderZ && z < upperBorderZ;                              // 这一套要求始终是一个以原点为中心的棋盘
    }

    // 计算: 某棋子放在 (px,pz) 且旋转为 rot 时, 它的 4 个槽位分别占据哪 4 个格子。
    // 先问棋子要它的 4 个"槽位偏移" (见 Piece.GetOffsets), 再整体加上锚点坐标。
    public Vector2Int[] GetCells(Piece p, int px, int pz, int rot)
    {
        // 视觉归零偏移由 Piece.SetVisualRotation 自己处理；Board 只关心最终逻辑格子。
        Vector2Int[] offs = p.GetOffsets(rot, out _);        // 4 个槽位相对锚点的偏移 (格子单位)
        Vector2Int[] res = new Vector2Int[offs.Length];     // 存放结果的数组
        for (int i = 0; i < offs.Length; i++)
            res[i] = new Vector2Int(px + offs[i].x, pz + offs[i].y);
        return res;
    }

    // 格子是否在棋盘范围内，输入格子坐标 (x,z)，返回 true/false。
    public bool InBounds(int x, int z) { return x >= leftBorderX && x < rightBorderX && z >= lowerBorderZ && z < upperBorderZ; }

    //专门的预览判定函数, 不改变棋盘状态, 用于鼠标拖动棋子时的实时预览
    public bool GhostlyTrial(Piece p, int px, int pz, int rot)
    {
        GhostlyRecover();
 
        Vector2Int[] cells = GetCells(p, px, pz, rot); // 先算出要占哪 4 格
        for (int i = 0; i < cells.Length; i++)
        {
            int cx = cells[i].x, cz = cells[i].y;
            if (!InBounds(cx, cz))
                return false;         // 任一格越界 -> 拒绝
        }

        bool usedOrNot = false;

        for(int i=0; i< cells.Length; i++)
        {
            int cx = cells[i].x, cz = cells[i].y;
            if (board[cx,cz].piece != null)
            {
                GhostlyMove(i, cx, cz, false);
                usedOrNot = true;
            }
            else
                GhostlyMove(i,cx, cz, true);
        }
        return !usedOrNot;
    }



    //将预览格子移动到指定位置, 并根据是否可放置改变颜色
    //注意: 与 CellToWorld 一致, 按"wCenter/hCenter"居中, 而不是全棋盘 W/H ——
    //      否则 boardStage > 0 时预览块位置会相对实际落子点偏移
    public void GhostlyMove(int ind, int ppx, int ppz, bool legible)
    {
        if(legible)
        {
            ghostBlocks[ind].GetComponent<Renderer>().material.color = new Color(0.5f, 1f, 0.5f, 0.5f);
        }
        else
        {
            ghostBlocks[ind].GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0.5f, 0.5f);
        }
        ghostBlocks[ind].transform.position = new Vector3(ppx - wCenter, 0.8f, ppz - hCenter);
    }

    //将预览格子恢复到初始位置，不在场景中显示
    public void GhostlyRecover()
    {
        for (int i = 0; i < 6; i++)
        {
            ghostBlocks[i].transform.position = new Vector3(ini.x + i, ini.y, ini.z);
        }
    }

    // ---- 放置 / 移除 -------------------------------------------------------------
    // 尝试把棋子 p 放到 (px,pz), 旋转 rot。
    // 规则: 全部 4 格必须在界内 (越界拒绝) 且全为空 (重叠拒绝)。
    // 失败时不做任何修改 (调用方可以安全回退); 成功时更新数组 + 棋子的坐标/旋转 + 场景位置。
    public bool TryPlace(Piece p, int px, int pz, int rot)
    {
        Vector2Int[] cells = GetCells(p, px, pz, rot); // 先算出要占哪 4 格
        for (int i = 0; i < cells.Length; i++)
        {
            int cx = cells[i].x, cz = cells[i].y;
            if (!InBounds(cx, cz)) return false;         // 任一格越界 -> 拒绝
            if (board[cx, cz].piece != null) return false;
        }
        // 校验全部通过, 开始落子:

        RemovePiece(p); // 先把棋子原来的占位清掉 (如果它之前放在别处), 避免自冲突
        for (int i = 0; i < cells.Length; i++)
        {
            board[cells[i].x, cells[i].y].piece = p; // 记录"这格被棋子 p 占据"
            board[cells[i].x, cells[i].y].slot = i;  // 记录"是 p 的第 i 个槽位"
        }
        p.bx = px; p.bz = pz; p.rot = rot; p.placed = true; // 更新棋子自身的数据
        p.SetPlacedTransform(px, pz, rot);                  // 同步棋子在场景里的位置/旋转
        return true;
    }

    // 把棋子从棋盘上拿下来: 清空它占据的所有格子, 并把 placed 标记为 false。
    // (拖动棋子时调用: 先"拿起来", 松手再 TryPlace 放回去)
    public void RemovePiece(Piece p)
    {
        if (!p.placed) return; // 本来就没在棋盘上, 直接返回
        Vector2Int[] cells = GetCells(p, p.bx, p.bz, p.rot); // 用棋子记录的旧位置算出格子
        for (int i = 0; i < cells.Length; i++)
        {
            int cx = cells[i].x, cz = cells[i].y;
            if (InBounds(cx, cz) && board[cx, cz].piece == p) // 只清"确实是它占的"格子
            {
                board[cx, cz].piece = null;
                board[cx, cz].slot = -1;
            }
        }
        p.placed = false;
    }

    // ---- 贯通区域判定 (连通分量 BFS) ---------------------------------------------
    // 规则已经改为依照老师要求的属性相连方可。
    //
    // 这里求的已经不是“指定入口到指定出口的一条最短路径”，而是：
    //   1. 用 BFS 找出棋盘上的每一个独立连通分量；
    //   2. 检查该分量是否同时接触有效的上边界入口和下边界出口；
    //   3. 把所有满足条件的分量完整加入 path，最后得到全部贯通区域的并集。
    //
    // 因而一个贯通分量里的分叉、环路以及会进水的死胡同支路也都会被计入。
    // 这是“整片已经通水的管网”语义，而不是“从中挑一条代表路线”的语义。
    //
    // 方法名和 out 参数名暂时保留为 FindPath/path，是为了不破坏 SaultUI 等现有调用，
    // 也方便以后恢复单点最短路径玩法；当前 path 实际表示“贯通区域中的全部格子”。
    public bool FindPath(out List<BoardCell> pathThrough, out int pathBeginCount)
    {
        pathThrough = new List<BoardCell>();
        List<BoardCell> pathBegin = new List<BoardCell>();

        // visited 是整次扫描共用的访问表。
        // 一个格子一旦归入某个连通分量，后续外层循环就不会再次搜索它，
        // 因此总工作量与有效棋盘格数量基本成正比。
        bool[,] visited = new bool[W, H];
        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };

        // 只扫描当前 boardStage 划定的有效棋盘范围，不处理外围被锁住的格子。
        for (int startX = leftBorderX; startX < rightBorderX; startX++)
        {
            for (int startZ = lowerBorderZ; startZ < upperBorderZ; startZ++)
            {
                // 空格不是管网的一部分；访问过则说明已经属于先前搜索过的分量。
                if (visited[startX, startZ] || board[startX, startZ].piece == null)
                    continue;

                BoardCell startCell = board[startX, startZ];
                if (startCell.piece.slotLetters[startCell.slot] != decideLetter) continue;

                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                List<BoardCell> component = new List<BoardCell>();

                // 这两个标记描述“当前整片连通分量”的边界接触情况。
                // entryX[x]/exitX[x] 允许以后把整条边界逐步收紧成部分区间或单点；
                // levelDifficulty == 0 时 RandomTeacher 会把有效宽度内的列全部打开。
                bool touchesEntry = false;
                bool touchesExit = false;

                queue.Enqueue(new Vector2Int(startX, startZ));
                visited[startX, startZ] = true;

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    BoardCell currentCell = board[current.x, current.y];
                    component.Add(currentCell);                                 //加进当前集合的物件

                    // upperBorderZ 是上边界外侧的排他坐标，所以最上方有效行需要 -1。
                    if(xyDecider == 1)
                    {
                        if (current.x == leftBorderX && entryZ[current.y])
                            touchesEntry = true;

                        if(current.x == rightBorderX - 1 && exitZ[current.y])
                            touchesExit = true;
                    }

                    else if(xyDecider == 0)
                    {
                        if (current.y == upperBorderZ - 1 && entryX[current.x])
                            touchesEntry = true;

                        // lowerBorderZ 本身就是最下方有效行。
                        if (current.y == lowerBorderZ && exitX[current.x])
                            touchesExit = true;
                    }
                    // 扩展当前分量。这里只有“相邻格是否符合条件”的既有连通规则，
                    // 不记录距离或父节点，因为现在既不求最短路，也不需要回溯单一路线。
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int nextX = current.x + dx[direction];
                        int nextZ = current.y + dz[direction];

                        if (!InBounds(nextX, nextZ) || visited[nextX, nextZ])
                            continue;                                           //出界或者已经看过了
                        BoardCell bc = board[nextX, nextZ];
                        if (bc.piece == null)
                            continue;                                           //如果就没有棋子
                        if (BFSvalid(bc) == false)
                            continue;
                        
                        visited[nextX, nextZ] = true;
                        queue.Enqueue(new Vector2Int(nextX, nextZ));
                    }
                }

                // 只有同时触及上、下开放边界的分量才真正贯通。
                // 不在发现第一个分量时 return：继续扫描才能收集棋盘上其他独立贯通区域。
                if (touchesEntry && touchesExit)
                    pathThrough.AddRange(component);
                else if (touchesEntry)
                    pathBegin.AddRange(component);
            }
        }

        pathBeginCount = pathBegin.Count;

        return pathThrough.Count > 0;
    }

    bool BFSvalid(BoardCell boardcell)
    {
        //初始判定——仅与统一判定而不是当下格子判定，确保准确。
        if (boardcell.piece.slotLetters[boardcell.slot]==decideLetter)
            return true;
        //特殊判定——SlotPerk和AttrValue2的事情，到时候再看。

        return false;
    }

    // ---- BFS预算预览 ---------------------------------------------------------------
    // 需求: 至少有一个连通分量从有效上边界贯通到有效下边界，
    //       且所有贯通分量并集里的 S 数值总和达到 TargetS。
    // 输出三个 out 参数: 是否通过、S 总和、给人看的提示文字。
    // 可以由预览和提交两者调取，实时更改UI最好，结算交给SaultUI。
    public void BFS(out int connectingPts, out bool pass, out string msg)
    {
        List<BoardCell> pathThrough;
        pass = false;
        //pathThrough记录从开始到结束连通的路线
        //pathBegin会记录从开始到结束联通的
        if (!FindPath(out pathThrough, out int pathBeginCount)) // path 现在是所有贯通区域的并集，而非一条最短路径
        {
            connectingPts = pathBeginCount;
            pass = connectingPts >= TargetS;
            msg = pass
            ? "未连通，采用通量面积PASS — " + connectingPts + " ≥ " + TargetS        //之后要改成血条UI
            : "未连通，采用通量面积FAIL — " + connectingPts + " < " + TargetS;
            return;
        }
        connectingPts = 0;
        //
        for (int i = 0; i < pathThrough.Count; i++)
        {
            BoardCell bc = pathThrough[i];
            if (bc.piece != null)
            {
                int absValue = Mathf.Abs(bc.piece.ValueOf(bc.piece.slotLetters[bc.slot]));
                connectingPts += absValue;
            }
        }
        pass = connectingPts >= TargetS;
        msg = pass
        ? "PASS —" + connectingPts + " ≥ " + TargetS            //之后要改成血条UI，和未连通共享一套判定
        : "FAIL —" + connectingPts + " < " + TargetS;
        return;
        
    }

    // ================= 视觉搭建 (BuildBoard) =================
    // 下面的内容属于"场景搭建工具": 由 SceneBuilder (编辑器菜单) 在编辑模式下调用,
    // 生成棋盘的外观。运行时也可以调用, 所以同时兼容两种环境。

    // 找一个可用的着色器 (Shader)。不同渲染管线里着色器名字不同:
    //   Built-in 管线 -> "Standard"
    //   URP 管线     -> "Universal Render Pipeline/Lit"
    // 本项目用的是 URP, 但这里按顺序逐个尝试以兼容各种情况,
    // 全都找不到时退回最基础的 Unlit/Color (纯色, 不受光照影响)。
    public static Shader FindShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("URP/Lit");
        if (s == null) s = Shader.Find("Legacy Shaders/Diffuse");
        if (s == null) s = Shader.Find("Unlit/Color");
        return s;
    }

    // 搭建棋盘的完整视觉:
    //   - 一块深色地面 (Plane): 接收阴影, 让棋子阴影有地方落
    //   - 20 个浅色地砖 (Cube): 摆成 5x4, 砖与砖之间的缝隙自然形成网格线
    //   - 四条深色细条: 棋盘外边框 (深色边缘线, 建筑模型风)
    //   - 入口/出口标记: 绿色小方块在入口格, 红色在出口格
    //   - 一个"隐形"的 BoxCollider: 全场景唯一的碰撞体, 是鼠标射线唯一的目标
    //     (棋子本身没有碰撞体, 这样射线永远只命中这一个 collider, 逻辑最简单)
    // 注意: 在编辑模式下运行 (菜单点击) 时 Application.isPlaying 为 false,
    // 所以删除组件要用 DestroyImmediate 而不是 Destroy (后者只在运行时有效)。
    public void BuildBoard()
    {
        // 兜底: 如果材质没被赋值, 现场 new 一个 (避免物体变粉红色/报错)
        if (matTile == null) matTile = new Material(FindShader()) { color = new Color(0.93f, 0.94f, 0.96f) };
        if (matEdge == null) matEdge = new Material(FindShader()) { color = new Color(0.13f, 0.14f, 0.17f) };
        if (matGround == null) matGround = new Material(FindShader()) { color = new Color(0.20f, 0.21f, 0.24f) };

        if (visualYet) return;

        // 地面 (接收阴影)
        GameObject ground = CreatePrimitive(PrimitiveType.Plane); // 生成一个平面体
        ground.name = "Ground";
        ground.transform.SetParent(transform, false); // 挂到棋盘物体下; false = 保持局部坐标不变
        ground.transform.localPosition = new Vector3(0f, -0.06f, 0.8f); // 略低于棋盘, 中心偏向托盘侧
        ground.transform.localScale = new Vector3(4f, 1f, 4f); // Plane 原始 10x10, 放大 4 倍 = 40x40
        SetMat(ground, matGround);

        // 20 个地砖 (浅色, 缝隙自然形成网格线)
        for (int x = leftBorderX; x < rightBorderX; x++)
        {
            for (int z = lowerBorderZ; z < upperBorderZ; z++)
            {
                GameObject tile = CreatePrimitive(PrimitiveType.Cube); // 生成一个立方体
                tile.name = "Tile_" + x + "_" + z;                    // 命名方便在 Hierarchy 里辨认
                tile.transform.SetParent(transform, false);
                tile.transform.localPosition = CellToWorld(x, z) + new Vector3(0f, 0.03f, 0f); // 中心在格心, 抬高 0.03 露出地面
                tile.transform.localScale = new Vector3(0.96f, 0.06f, 0.96f); // 0.96 而非 1, 留出缝隙
                SetMat(tile, matTile);
            }
        }

        float hh = upperBorderZ-lowerBorderZ;
        float ww = rightBorderX-leftBorderX;
        float h0 = 0;
        float w0 = 0;

        Debug.Log(hh+" "+ww+" "+h0+" "+w0);

        // 深色边框 (四条细立方体围一圈)
        GameObject bN = CreatePrimitive(PrimitiveType.Cube);
        bN.name = "BorderN"; bN.transform.SetParent(transform, false);
        bN.transform.localPosition = new Vector3(w0, 0.03f, hh * 0.5f + 0.04f);   // 北边 (z 正方向)
        bN.transform.localScale = new Vector3(ww + 0.16f, 0.08f, 0.08f); SetMat(bN, matEdge);

        GameObject bS = CreatePrimitive(PrimitiveType.Cube);
        bS.name = "BorderS"; bS.transform.SetParent(transform, false);
        bS.transform.localPosition = new Vector3(w0, 0.03f, -(hh * 0.5f + 0.04f)); // 南边
        bS.transform.localScale = new Vector3(ww + 0.16f, 0.08f, 0.08f); SetMat(bS, matEdge);

        GameObject bE = CreatePrimitive(PrimitiveType.Cube);
        bE.name = "BorderE"; bE.transform.SetParent(transform, false);
        bE.transform.localPosition = new Vector3(ww * 0.5f + 0.04f, 0.03f, h0);    // 东边
        bE.transform.localScale = new Vector3(0.08f, 0.08f, hh + 0.16f); SetMat(bE, matEdge);

        GameObject bW = CreatePrimitive(PrimitiveType.Cube);
        bW.name = "BorderW"; bW.transform.SetParent(transform, false);
        bW.transform.localPosition = new Vector3(-(ww * 0.5f + 0.04f), 0.03f, h0); // 西边
        bW.transform.localScale = new Vector3(0.08f, 0.08f, hh + 0.16f); SetMat(bW, matEdge);

        // 唯一的射线目标: 覆盖棋盘 + 下方拖放区 (tray)
        // 为什么这样设计: 输入流程要求"鼠标 -> 射线 -> 世界点 -> 棋盘坐标",
        // 一个覆盖整个交互区域的大 BoxCollider 让 raycast 永远有命中, 逻辑最简。
        GameObject col = new GameObject("BoardCollider");
        col.transform.SetParent(transform, false);
        BoxCollider bc = col.AddComponent<BoxCollider>(); // 挂上盒形碰撞体 (它自己就是一个组件)
        bc.center = new Vector3(0f, 0.05f, 0.8f);  // 中心略偏 z 正方向, 把下方托盘区也罩进来
        bc.size = new Vector3(W + 0.4f, 0.1f, H + 2.0f); // 横向盖满棋盘, 纵向额外 +2 覆盖托盘
        visualYet = true;
    }

    // 生成一个基础几何体并去掉它自带的 Collider。
    // CreatePrimitive 生成的物体默认带碰撞体, 而本原型只需要"棋盘那一个大 collider"
    // 作为射线目标, 所以其余的全拆掉, 防止射线被别的东西挡住。
    static GameObject CreatePrimitive(PrimitiveType t)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        Collider c = g.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying) Object.Destroy(c); // 运行期: 延迟到帧末销毁
            else Object.DestroyImmediate(c);              // 编辑模式: 立即销毁
        }
        return g;
    }

    // 给物体换上指定材质 (sharedMaterial 会同时影响所有用同一材质的物体, 省内存)
    static void SetMat(GameObject g, Material m)
    {
        Renderer r = g.GetComponent<Renderer>(); // Renderer 负责把网格真正画出来
        if (r != null) r.sharedMaterial = m;
    }
}
