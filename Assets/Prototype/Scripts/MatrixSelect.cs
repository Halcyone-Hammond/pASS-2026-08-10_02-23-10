using UnityEngine;
using UnityEngine.UI;


public class MatrixSelect : MonoBehaviour
{
    public Button[] buttons;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public Button buttonGuide(int x, int z)
    {
        return buttons[x + z * 4];
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
