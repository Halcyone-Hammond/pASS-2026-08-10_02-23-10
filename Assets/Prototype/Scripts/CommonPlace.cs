using UnityEngine;

//所有共用的组件都放在这里, 方便其他脚本引用
public class CommonPlace : MonoBehaviour
{
    public static CommonPlace Instance;

    void Awake()
    {
        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
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

    // Update is called once per frame
    void Update()
    {
        
    }
}
