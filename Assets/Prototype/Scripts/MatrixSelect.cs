
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MatrixSelect : MonoBehaviour
{
    [SerializeField] public Button[] buttons;
    [SerializeField] GameObject uiScript;
    [SerializeField] public RectTransform btnSelect;
    SaultUI ui;

    public class buttonData
    {
        public int value;
        public int slot = -1;                                           //slot就是i，-1就是没槽位
        public int letter;
        public Color colour;
    }
    buttonData[] buttonDatas = new buttonData[16];

    void Awake()
    {
        ui = uiScript.GetComponent<SaultUI>();
        buttonDatas = new buttonData[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            buttonDatas[i] = new buttonData();
            if (buttons[i] == null) continue;
            buttonDatas[i].colour = Color.lightGray;
            buttonDatas[i].value = 0;

            int buttonIndex = i; // Capture the current button index for the closure
            buttons[i].onClick.AddListener(() => SelectButton(buttonIndex));
        }
        buttonUpdate();
    }

    private void Start()
    {
        
    }

    //这个不是tryselect，这个选中的是矩阵的按钮
    void SelectButton(int buttonIndex)
    {
        int slot = buttonDatas[buttonIndex].slot;                       //用于返回在SaultUI和Piece能用的指针
        if (slot < 0) return; // 如果 slot 小于 0，说明没有有效的按钮数据，直接返回
        RectTransform target = buttons[buttonIndex].GetComponent<RectTransform>();

        // 将 btnSelect 移动到目标按钮的位置
        btnSelect.anchoredPosition = target.anchoredPosition;           // UI 里是用 anchoredPosition 来设置位置的
        btnSelect.gameObject.SetActive(true);                           // 显示选中框
        ui.SlotSelect(slot);                                            // 调用 SaultUI 的 Selectslot 方法
    }


    private void buttonUpdate()
    {
        for(int i = 0; i< buttons.Length; i++)
        {
            if(buttons[i] == null) continue;
            buttons[i].GetComponentInChildren<TMP_Text>().text = buttonDatas[i].value.ToString();
            buttons[i].image.color = buttonDatas[i].colour;
        }
    }

    public void buttonOn(int x, int z, int slot, int value, int letter, Color color)
    {
        int buttonIndex = x + z * 4;
        if (buttonIndex < 0 || buttonIndex >= buttons.Length) return;
        buttons[buttonIndex].gameObject.SetActive(true);
        buttonDatas[buttonIndex].slot = slot;
        buttonDatas[buttonIndex].letter = letter;
        buttonDatas[buttonIndex].value = value;
        buttonDatas[buttonIndex].colour = color;
        buttonUpdate();
        return;
    }

    public void buttonOff(int x, int z)
    {
        int buttonIndex = x + z * 4;
        if (buttonIndex < 0 || buttonIndex >= buttons.Length) return;
        buttonDatas[buttonIndex].colour = Color.lightGray;
        buttonDatas[buttonIndex].letter = 0;
        buttonDatas[buttonIndex].value = 0;
        buttonDatas[buttonIndex].slot = -1;
        buttonUpdate();
        buttons[buttonIndex].gameObject.SetActive(false);
        return;
    }

    public void ClearButtons()
    {
        for(int i = 0; i < buttons.Length; i++)
        {
            if(buttonDatas[i] != null)
            {
                buttonDatas[i].slot = -1; buttonDatas[i].letter = 0; buttonDatas[i].value = 0; buttonDatas[i].colour = Color.lightGray;
            }
            if (buttons[i] != null)
            {
                buttons[i].gameObject.SetActive(false);
            }
        }
        btnSelect.gameObject.SetActive(false);
        //buttonUpdate();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
