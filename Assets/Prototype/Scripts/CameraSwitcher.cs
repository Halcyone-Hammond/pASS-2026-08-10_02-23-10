using UnityEngine;
using UnityEngine.InputSystem; // 新版输入系统 (Keyboard / Mouse 等)

// ============================================================
// CameraSwitcher —— 双视角切换
// ============================================================
// 需求: 提供两个正交 (orthographic) 视图:
//   1. 俯视图 (top): 正上方往下看, 像平面图/总平面
//   2. 轴测图 (axi): 从斜上方看, 有立体感的轴测视角
// 切换方式: 按 Space 键 或 点 UI 里的"切换视角"按钮。
// 实现要点:
//   * 只有一个 Camera, 用两个"姿态" (位置+旋转+缩放) 描述两个视角
//   * 切换时用插值 (Lerp/Slerp) 做平滑动画, 而不是瞬间跳变
// ============================================================
public class CameraSwitcher : MonoBehaviour
{
    // 一个"相机姿态": 位置 + 旋转 + 正交相机的视野大小
    // [System.Serializable] 让 struct 能显示在 Inspector 里方便调参
    // struct (值类型) 在这里够用: 就是一组数据, 没有继承需求
    [System.Serializable]
    public struct Pose
    {
        public Vector3 pos;    // 相机位置
        public Quaternion rot; // 相机朝向 (四元数, Unity 里旋转的底层表示)
        public float size;     // orthographicSize: 正交相机的"半高视野" (单位: 世界单位)
    }

    public Pose top;        // 俯视姿态 (由 SceneBuilder 填入数值)
    public Pose axi;        // 轴测姿态
    public bool isTop = true; // 当前是否处于俯视 (true=俯视, false=轴测)

    bool switching; // 是否正在播放切换动画
    Pose start;     // 动画起点姿态 (按下切换那一刻的姿态)
    float t;        // 动画进度 0~1

    // Start: 开场就位到俯视姿态
    void Start()
    {
        ApplyPose(isTop ? top : axi);
    }

    // Update: 每帧检测 Space 键 + 推进切换动画
    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) Toggle(); // Space 刚按下 -> 切换

        if (!switching) return; // 没在切换就直接结束本帧

        // 动画推进: t 随时间增加, 乘以 1.5 控制切换速度
        t += Time.deltaTime * 1.5f; // deltaTime = 上一帧耗时(秒), 保证动画速度与帧率无关
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)); // 平滑缓动: 开头结尾慢, 中间快
        Pose target = isTop ? top : axi; // 要飞向的目标姿态
        Camera c = GetComponent<Camera>();
        if (c == null) return;
        c.transform.position = Vector3.Lerp(start.pos, target.pos, k);        // 位置线性插值
        c.transform.rotation = Quaternion.Slerp(start.rot, target.rot, k);    // 旋转球面插值 (更自然)
        c.orthographicSize = Mathf.Lerp(start.size, target.size, k);          // 视野大小插值
        if (t >= 1f) switching = false; // 动画播完, 结束
    }

    // 切换视角 (被 Space 或 UI 按钮调用)
    public void Toggle()
    {
        Camera c = GetComponent<Camera>();
        if (c == null) return;
        // 记录起点 = 当前姿态 (保证从任何中间位置都能平滑继续)
        start = new Pose { pos = c.transform.position, rot = c.transform.rotation, size = c.orthographicSize };
        isTop = !isTop;   // 翻转目标
        switching = true; // 开始动画
        t = 0f;
    }

    // 直接落到某个姿态 (无动画), 搭建场景时用来做初始定位
    public void ApplyPose(Pose p)
    {
        Camera c = GetComponent<Camera>();
        if (c == null) return;
        c.transform.position = p.pos;
        c.transform.rotation = p.rot;
        c.orthographicSize = p.size;
        switching = false;
    }
}
