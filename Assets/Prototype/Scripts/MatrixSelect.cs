using UnityEngine;
using UnityEngine.UI;


public class MatrixSelect : MonoBehaviour
{
    public Button[] buttons;

    void Awake()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i])
            {
                buttons[i].image.color = Color.lightGray;
            }
        }
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
