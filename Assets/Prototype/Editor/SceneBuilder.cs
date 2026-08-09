using UnityEditor;                 // 编辑器 API: MenuItem / AssetDatabase 等
using UnityEditor.SceneManagement; // 编辑器场景管理: EditorSceneManager (菜单里建/存场景)
using UnityEngine;
using UnityEngine.SceneManagement; // Scene 类型 (NewScene 的返回值)
using UnityEngine.UI;              // Canvas / CanvasScaler / GraphicRaycaster 等 UI 组件 (属于 UnityEngine.UI 命名空间)

// ============================================================
// SceneBuilder —— 一键搭建原型场景的编辑器工具
// ============================================================
// 这是个"编辑器脚本" (放在名为 Editor 的文件夹里), 不属于游戏逻辑,
// 只负责在编辑模式下把整个场景搭好, 让你少做几十步手动操作。
//
// 用法: 菜单栏 Tools -> Build Simulator Prototype
// 它会:
//   1. 新建一个空场景
//   2. 创建材质资源 (Assets/Materials/*.mat, 存成文件, 场景重载不丢)
//   3. 设置环境光
//   4. 创建平行光 (软阴影)
//   5. 创建棋盘 (调 Board.BuildBoard: 地砖/边框/地面/入口出口标记/唯一的 BoxCollider)
//   6. 创建 L/T/I/O 四个棋子, 停在托盘区
//   7. 创建主相机 (正交, 俯视), 挂 CameraSwitcher 并填入两个视角姿态
//   8. 创建 Canvas + SaultUI (SAUL 面板) + EventSystem (新输入系统版)
//   9. 创建 Game 物体挂 InputController, 并互相引用
//   10. 保存场景到 Assets/Scenes/Simulator.unity
// ============================================================
public static class SceneBuilder
{
    // [MenuItem] 把静态方法注册成编辑器菜单项
    [MenuItem("Tools/Build Simulator Prototype")]
    public static void Build()
    {
        // 新建空场景 (Single = 替换当前场景; 若当前场景有未保存改动 Unity 会先弹窗询问)
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 确保目标文件夹存在 (不存在就创建)
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Scenes");

        // 材质 (存为资源文件, 场景重载不丢)
        Material matTile = CreateMat("MatTile", new Color(0.93f, 0.94f, 0.96f));   // 地砖: 浅色
        Material matEdge = CreateMat("MatEdge", new Color(0.13f, 0.14f, 0.17f));   // 边框: 深色
        Material matGround = CreateMat("MatGround", new Color(0.20f, 0.21f, 0.24f)); // 地面: 深灰
        Material matEntry = CreateMat("MatEntry", new Color(0.20f, 0.68f, 0.32f)); // 入口标记: 绿
        Material matExit = CreateMat("MatExit", new Color(0.85f, 0.28f, 0.24f));   // 出口标记: 红
        Material[] tints = new Material[4]; // 四种字母色 (S/A/U/L 各一)
        tints[Piece.S] = CreateMat("MatS", new Color(0.36f, 0.58f, 0.95f)); // 蓝
        tints[Piece.A] = CreateMat("MatA", new Color(0.45f, 0.78f, 0.45f)); // 绿
        tints[Piece.U] = CreateMat("MatU", new Color(0.98f, 0.78f, 0.30f)); // 黄
        tints[Piece.L] = CreateMat("MatL", new Color(0.95f, 0.45f, 0.42f)); // 红

        // 环境光 (新场景没有天空盒, 手动给一点环境光, 否则背光面全黑)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; // 均匀环境光
        RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.60f);

        // 平行光 + 软阴影 (模拟日光; 阴影让棋盘更有建筑模型的立体感)
        GameObject lightGO = new GameObject("Directional Light");
        Light lt = lightGO.AddComponent<Light>();
        lt.type = LightType.Directional;   // 平行光: 方向性光源, 不衰减
        lt.shadows = LightShadows.Soft;    // 软阴影
        lt.intensity = 1.15f;
        lt.color = new Color(1f, 0.98f, 0.95f); // 略微暖白
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f); // 斜 50° 照下来

        // 棋盘
        GameObject boardGO = new GameObject("Board");
        Board board = boardGO.AddComponent<Board>(); // 挂 Board 组件 (数据 + 视觉)
        board.matTile = matTile;   // 把材质引用塞给它
        board.matEdge = matEdge;
        board.matGround = matGround;
        board.matEntry = matEntry;
        board.matExit = matExit;
        board.BuildBoard(); // 编辑模式下直接生成棋盘视觉 (保存进场景)

        // 4 个棋子, 停靠在棋盘下方的托盘区 (z=3.3, 在棋盘 4 行之外)
        Shape[] shapes = { Shape.L, Shape.T, Shape.I, Shape.O };
        float[] trayX = { -1.5f, -0.5f, 0.5f, 1.5f }; // 托盘从左到右排开
        for (int i = 0; i < shapes.Length; i++)
        {
            GameObject pg = new GameObject("Piece " + shapes[i]);
            Piece piece = pg.AddComponent<Piece>();
            piece.board = board; // 引用棋盘 (Piece.Start 里也有兜底查找)
            piece.Init(shapes[i], matEdge, matTile, tints, new Vector3(trayX[i], 0f, 3.3f));
            board.RegisterPiece(piece); // 登记进 allPieces 清单
        }

        // 主相机: 正交 (orthographic), 初始为俯视姿态
        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera"; // 打上标签, 这样 Camera.main 才能找到它
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;                 // 正交投影 (没有透视近大远小)
        cam.clearFlags = CameraClearFlags.SolidColor; // 背景用纯色 (不要天空盒)
        cam.backgroundColor = new Color(0.88f, 0.90f, 0.93f); // 浅蓝灰背景
        camGO.AddComponent<AudioListener>();     // 场景里需要有唯一的音频监听器
        CameraSwitcher sw = camGO.AddComponent<CameraSwitcher>();
        // 俯视: 正上方往下看, 视野半高 3.4 (刚好罩住 4 行高的棋盘)
        sw.top.pos = new Vector3(0f, 9.5f, 0.6f);
        sw.top.rot = Quaternion.Euler(90f, 0f, 0f); // 绕 X 转 90°: 视线垂直向下
        sw.top.size = 3.4f;
        // 轴测: 从斜上前方看原点
        sw.axi.pos = new Vector3(7.5f, 8.2f, -7.5f);
        sw.axi.rot = Quaternion.LookRotation(new Vector3(-7.5f, -8.2f, 7.5f)); // 朝向原点
        sw.axi.size = 5.6f;
        sw.ApplyPose(sw.top); // 先落到俯视

        // UI (新输入系统版事件模块)
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; // 屏幕空间覆盖: UI 浮在画面上层
        canvasGO.AddComponent<CanvasScaler>();   // 分辨率缩放适配
        canvasGO.AddComponent<GraphicRaycaster>(); // UI 接收点击
        SaultUI ui = canvasGO.AddComponent<SaultUI>(); // SAUL 面板逻辑 (UI 元素运行期自建)

        // EventSystem: UI 事件总调度器; 本项目用新输入系统,
        // 所以事件模块必须用 InputSystemUIInputModule (不是旧的 StandaloneInputModule)
        GameObject esGO = new GameObject("EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // 控制器
        GameObject gameGO = new GameObject("Game");
        InputController ic = gameGO.AddComponent<InputController>();
        ic.board = board; // 引用棋盘
        ic.ui = ui;       // 引用面板

        // 保存场景 (覆盖式保存到 Assets/Scenes/Simulator.unity)
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Simulator.unity");
        Debug.Log("Simulator prototype built → Assets/Scenes/Simulator.unity (press Play to test)");
    }

    // 确保父目录下存在名为 name 的子目录 (AssetDatabase 管的是 Assets 内的虚拟路径)
    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    // 创建材质并保存为资源文件:
    // 如果不存成文件, 场景里引用的是"内存材质", 重启 Unity 后引用会丢、物体变粉。
    static Material CreateMat(string name, Color c)
    {
        Shader sh = Board.FindShader(); // 自动选兼容当前渲染管线的着色器 (见 Board.cs)
        if (sh == null) sh = Shader.Find("Unlit/Color"); // 最后的兜底
        Material m = new Material(sh);
        m.color = c; // 设置颜色 (Standard/URP Lit 都会把 color 映射到主颜色)
        AssetDatabase.CreateAsset(m, "Assets/Materials/" + name + ".mat"); // 存成 .mat 资源
        return m;
    }
}
