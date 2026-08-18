using UnityEngine;
using TMPro; // TextMeshPro 组件 (3D 世界里的文字, 不是 UI 文字)

// 棋子形状枚举: 四种经典"四格骨牌" (tetromino) 形状
public enum Shape { L, T, I, O }

// ============================================================
// Piece —— 一个棋子
// ============================================================
// 规则要点:
//   * 每个棋子恰好 4 个"槽位" (slot), 也就是 4 个格子
//   * 每个槽位分配一个字母: S(结构) / A(外观) / U(功能) / L(升降), 四个字母各一个
//   * 字母"粘在"槽位上: 棋子移动/旋转时字母跟着槽位走, 旋转绝不会重排字母
//   * 每个字母还有自己的数值 (初始都是 1)
// 数据:
//   * 槽位几何: 用 4 个"局部偏移"描述 (锚点 (0,0) 是棋子的参考点)
//   * 旋转: rot 0~3 代表旋转 0/90/180/270 度; 4 种旋转的偏移由基础布局旋转得到
//   * 字母分配: slotLetters[槽位] = 字母; 数值: attrValues[字母] = 数值
// 视觉:
//   * 每个槽位 = 深色底座(当黑边) + 字母色顶面 + 一个 TextMesh 字母标签
//   * 视觉元素都是本物体的子物体, 所以跟着棋子一起移动/旋转 ——
//     这正是"字母随槽位走"的视觉保证
// ============================================================
public class Piece : MonoBehaviour
{
    // 字母编号常量: 用 int 而不是 char, 方便直接当数组下标用
    public const int S = 0, A = 1, U = 2, L = 3;
    public static readonly string Letters = "SAUL"; // 编号 -> 字符 的查表 (0=S, 1=A, ...)
    [SerializeField] TMP_FontAsset FontAsset; //字体资源 (在编辑器里拖入)

    // 每种形状的"基础布局" (旋转 0 度时): 4 个槽位的局部坐标 (x, z), 单位 = 1 格。
    // 锚点 (0,0) 一定在形状里, 因为旋转都是绕着它转的。
    //   L 形: 横三格 + 右端下方一格
    //   T 形: 横三格 + 中间上方一格
    //   I 形: 一长条四格
    //   O 形: 2x2 方块
    // Vector2Int = 整数二维向量 (x, y), 这里把 y 当 z 用
    static readonly Vector2Int[][] BASE = new Vector2Int[4][]
    {
        new Vector2Int[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(2,1) }, // L
        new Vector2Int[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(1,1) }, // T
        new Vector2Int[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(3,0) }, // I
        new Vector2Int[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) }, // O
    };

    // ---- 数据字段 ---------------------------------------------------------------
    public Shape shape;   // 什么形状 (L/T/I/O)
    public Board board;   // 所属棋盘 (用于坐标换算等)
    public int rot;       // 旋转 0/1/2/3 (每档 90 度)
    public int bx, bz;    // 锚点所在的格子坐标 (只有 placed 时才有效)
    public bool placed;   // 是否已放到棋盘上 (false = 在托盘里或正在被拖动)

    public int[] slotLetters = new int[] { 0, 1, 2, 3 }; // 槽位 -> 字母 (S=0,A=1,U=2,L=3)，根据指针对应字母
    public int[] attrValues = new int[] { 1, 1, 1, 1 };  // 字母 -> 数值 (初始全 1)，根据字母对应

    // 视觉材质: matDark 深色底座 / matLight 顶面(会被字母色替换) / matTints 四种字母色
    public Material matDark, matLight;
    public Material[] matTints;                                                             //根据字母对应

    [SerializeField] Vector3 trayPosition; // 未放置时停靠的托盘位置 (场景搭建时指定)

    Renderer[] topRends = new Renderer[4]; // 4 个槽位顶面的渲染器 (换字母色用)
    TMP_Text[] labels = new TMP_Text[4];   // 4 个槽位的文字标签 (显示 S/A/U/L)

    // 形状名字符串 (给 UI 显示用), 如 "L"、"T"
    public string shapeName { get { return shape.ToString(); } }

    // ---- 旋转与槽位几何 -----------------------------------------------------------
    // 返回该形状在旋转 r 下的 4 个槽位偏移 (棋盘格子坐标, 锚点 = (0,0))。
    // 做法: 把基础布局的每个点绕原点顺时针转 90° 共 r 次,
    //       然后整体平移, 让最小坐标归零 (保证坐标非负、锚点仍落在 (0,0))。
    // 旋转公式 (x, z) -> (z, -x) 是 90° 顺时针旋转的标准数学。
    public Vector2Int[] GetOffsets(int r)
    {
        Vector2Int[] src = BASE[(int)shape];   // 取出这种形状的基础布局
        Vector2Int[] pts = new Vector2Int[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            int x = src[i].x, y = src[i].y;
            for (int k = 0; k < r; k++) { int nx = y; y = -x; x = nx; } // 顺时针 90° 转一次
            pts[i] = new Vector2Int(x, y);
        }
        // 找出所有点里的最小 x 和最小 z, 用于整体平移归零
        int minx = int.MaxValue, miny = int.MaxValue;
        for (int i = 0; i < pts.Length; i++)
        {
            minx = Mathf.Min(minx, pts[i].x);
            miny = Mathf.Min(miny, pts[i].y);
        }
        // 每个点减去最小值: 形状完全不变, 但坐标变成非负, 锚点落在 (0,0)
        for (int i = 0; i < pts.Length; i++) pts[i] -= new Vector2Int(minx, miny);
        return pts;
    }

    // ---- 字母 / 数值 的读写接口 ---------------------------------------------------
    public int LetterAt(int slot) { return slotLetters[slot]; }          // 槽位 -> 字母
    public int ValueOf(int letter) { return attrValues[letter]; }        // 字母 -> 数值
    public void SetValue(int letter, int v) { attrValues[letter] = v; }  // 设置某字母的数值

    private void Awake()
    {

        if(!placed)
        {
            trayPosition = transform.position; // 托盘位置 = 初始位置 (编辑器里放好棋子后, 运行时记住它)
        }
        EnsureVisualData();
        EnsureTmpLabels();
        RefreshVisuals(); 
    }

    void EnsureVisualData()
    {
        bool fH = TryCacheExistingVisuals();
    }

    bool TryCacheExistingVisuals()
    {
        bool fullHouse = true;
        int index = 0;
        foreach(Transform child in transform)
        {
            if(child.name!="Top")
            {
                continue;
            }
            if(child.TryGetComponent(out Renderer renderer))
            {
                if (index<topRends.Length)
                {
                    topRends[index] = renderer;
                    index++;
                }
                else fullHouse = false;
            }
        }
        return fullHouse;
    }

    void EnsureTmpLabels()
    {
        Vector2Int[] offs = GetOffsets(0); // 用基础布局 (rot 0) 的偏移定位子物体 
        for (int i = 0; i < 4; i++)
        {
            Vector3 labelPos = new Vector3(offs[i].x, 0f, offs[i].y);

            GameObject go = new GameObject($"Label_{i}",typeof(RectTransform));

            go.transform.SetParent(transform, false);
            go.transform.localPosition =
                labelPos + new Vector3(0f, 0.37f, 0f);
            go.transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();

            tmp.font = FontAsset;
            tmp.fontSize = 5;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.07f, 0.08f, 0.12f);

            labels[i] = tmp;
        }
    }

    // 兜底逻辑: 如果棋盘引用没被赋值 (比如有人手动把棋子拖进别的场景),
    // 自动找到场景里的 Board 并登记自己, 保证 allPieces 清单完整。
    void Start()
    {
        if (board == null) board = FindAnyObjectByType<Board>();
        if (board != null) board.RegisterPiece(this);
    }

    // 初始化: 由 SceneBuilder (编辑器菜单) 在搭建场景时调用。
    // 传入形状、材质、托盘位置; 内部重置字母分配与数值、生成视觉、停到托盘。
    public void Init(Shape s, Material dark, Material light, Material[] tints, Vector3 trayPos)
    {
        shape = s;
        rot = 0;          // 初始不旋转
        placed = false;   // 初始在托盘里
        slotLetters = new int[] { 0, 1, 2, 3 }; // 槽位 0..3 依次放 S A U L
        attrValues = new int[] { 1, 1, 1, 1 };  // 四个字母的数值初始都是 1
        matDark = dark;
        matLight = light;
        matTints = tints;
        trayPosition = trayPos;
        BuildVisual();  // 生成 4 个槽位的立体外观
        GoToTray();     // 摆到托盘位置
    }

    // 回托盘: 撤销放置状态, 回到初始停靠点, 旋转归零
    public void GoToTray()
    {
        placed = false;
        rot = 0;
        Debug.Log($"GoToTray: {shapeName} -> {trayPosition}");
        transform.position = trayPosition;
        SetVisualRotation(0);
    }

    // 设置物体的世界旋转: 绕 Y 轴转 r*90 度。
    // 视觉上的 4 个槽位是物体的子物体, 会跟着物体一起转, 所以"转物体 = 转槽位"。
    public void SetVisualRotation(int r)
    {
        transform.rotation = Quaternion.Euler(0f, r * 90f, 0f);
    }

    // 放置后同步场景位置: 锚点格子 -> 世界坐标, 并应用旋转。
    // (x-2, 0, z-1.5) 是格子尺寸=1 时的快速硬编码换算; 有棋盘引用时走 Board.CellToWorld。
    public void SetPlacedTransform(int x, int z, int r)
    {
        Vector3 wp = new Vector3(x - 2f, 0f, z - 1.5f);
        if (board != null) wp = board.CellToWorld(x, z);
        transform.position = wp;
        SetVisualRotation(r);
    }

    // 选中高亮: 整体放大 6% (简单直观; 用 localScale 而不是改位置, 不影响格子对齐)
    public void SetSelected(bool sel)
    {
        transform.localScale = sel ? new Vector3(1.06f, 1.06f, 1.06f) : Vector3.one;
    }

    // 刷新 4 个槽位的显示: 顶面换成该槽位字母的颜色, 标签文字换成该字母。
    // 在"字母被重新分配"后调用, 保证视觉跟着数据走 (数据驱动视觉)。
    public void RefreshVisuals()
    {
        for (int i = 0; i < 4; i++)
        {
            if (topRends[i] != null && matTints != null)
                topRends[i].sharedMaterial = matTints[slotLetters[i]]; // 按字母换顶面颜色
            if (labels[i] != null)
            {
                int theLetter = slotLetters[i];
                labels[i].text = attrValues[theLetter].ToString(); // 按数值换标签文字
            }
        }
    }

    // 生成每个槽位的三层视觉: 深色底座(当黑边) + 字母色顶面 + 浮在顶上的字母标签。
    // 全部做成当前物体的子物体 (SetParent), 所以会跟着棋子一起移动/旋转。
    void BuildVisual()
    {
        Vector2Int[] offs = GetOffsets(0); // 用基础布局 (rot 0) 的偏移定位子物体
        for (int i = 0; i < 4; i++)
        {
            Vector3 lp = new Vector3(offs[i].x, 0f, offs[i].y); // 槽位局部坐标 (y 稍后加)

            // 底座: 略大的深色方块垫在下面, 露出边缘形成"深色边线"效果
            GameObject b = Prim("Base", lp + new Vector3(0f, 0.12f, 0f), new Vector3(1.02f, 0.12f, 1.02f), matDark);
            // 顶面: 略小的浅色方块, 颜色由字母决定 (RefreshVisuals 会换)
            GameObject t = Prim("Top", lp + new Vector3(0f, 0.26f, 0f), new Vector3(0.94f, 0.16f, 0.94f), matLight);
            topRends[i] = t.GetComponent<Renderer>(); // 记住顶面渲染器, 之后好换色

            // 字母标签: 传统 TextMesh (3D 世界里的文字, 不是 UI 文字), 平躺在顶面上
            GameObject lb = new GameObject("Label");
            lb.transform.SetParent(transform, false);
            lb.transform.localPosition = lp + new Vector3(0f, 0.37f, 0f); // 浮在顶面上方
            lb.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 绕 X 转 90°, 让文字面朝上
        }
        RefreshVisuals(); // 按当前字母分配上色、写字
    }

    // 生成一个"立方体子物体"的小工具: 设名字、父物体、局部位置、缩放、材质,
    // 并去掉它自带的 Collider (本原型只用棋盘那一个大 collider 做射线)。
    public GameObject Prim(string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(transform, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        Collider c = g.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying) Object.Destroy(c); // 运行期用 Destroy (延迟销毁)
            else Object.DestroyImmediate(c);              // 编辑模式用 DestroyImmediate (立即销毁)
        }
        Renderer r = g.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
        return g;
    }
}