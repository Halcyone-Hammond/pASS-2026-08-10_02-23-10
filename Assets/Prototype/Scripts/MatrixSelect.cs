
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MatrixSelect : MonoBehaviour
{
    [SerializeField] public Button[] buttons;
    [SerializeField] GameObject uiScript;
    [SerializeField] public RectTransform btnSelect;
    SaultUI ui;

    int draggingSlot = -1; // 当前拖动的槽位索引，-1表示没有拖动
    int draggingButtonIndex = -1; // 当前拖动的按钮索引，-1表示没有拖动
    bool isDragging = false; // 是否正在拖动按钮（防止onClick和onDrag冲突）

    public class buttonData
    {
        public int value;
        public int slot = -1;                                           //slot就是Piece里的i，-1就是没槽位
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

            EventTrigger eventTrigger = buttons[i].gameObject.AddComponent<EventTrigger>();

            var begin = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            begin.callback.AddListener(ev => OnBeginDrag(buttonIndex,(PointerEventData)ev));
            eventTrigger.triggers.Add(begin);

            var end = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            end.callback.AddListener(ev => OnEndDrag ((PointerEventData)ev));
            eventTrigger.triggers.Add(end);
        }
        buttonUpdate();
    }

    void OnBeginDrag(int buttonIndex, PointerEventData eventData)
    {
        int theSlot = buttonDatas[buttonIndex].slot;
        if (theSlot <0) return; // 如果 slot 小于 0，说明没有有效的按钮数据，直接返回
        draggingSlot = theSlot;
        draggingButtonIndex = buttonIndex;         // 矩阵里按钮的索引
        isDragging = true;
        buttons[buttonIndex].interactable = false; // 禁用按钮的交互，防止点击事件触发
    }

    void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return; // 如果没有拖动，直接返回
        if (draggingButtonIndex >=0 && buttons[draggingButtonIndex] != null)
            buttons[draggingButtonIndex].interactable = true; // 恢复按钮的交互
        int targetSlot = HitTestSlot(eventData); // 检测拖动结束时的槽位

        if(targetSlot >= 0 && targetSlot != draggingSlot)
        {
            // 调用 SaultUI 的 SwapSlot 方法，待定
            ui.SwapSlot(draggingSlot, targetSlot);
        }

        draggingSlot = -1;
        draggingButtonIndex = -1;
        isDragging = false;
    }

    int HitTestSlot(PointerEventData eventData)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for(int i=0; i< results.Count; i++)
        {
            //只选有button那一层的物件
            Button hit = results[i].gameObject.GetComponentInParent<Button>();
            if (hit != null)
            {
                for (int j = 0; j<buttons.Length; j++)
                {
                    if (buttons[j] == hit)
                            return buttonDatas[j].slot;
                }
            }
        }
        return -1; // 暂时返回 -1，表示没有命中任何槽位
    }

    private void Start()
    {
        
    }

    //这个不是tryselect，这个选中的是矩阵的按钮
    void SelectButton(int buttonIndex)
    {
        if (isDragging)
        {
            return;   
        }
        int slot = buttonDatas[buttonIndex].slot;                       //用于返回在SaultUI和Piece能用的指针
        if (slot < 0) return;                                           // 如果 slot 小于 0，说明没有有效的按钮数据，直接返回
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

    public void buttonOff()
    {
        for(int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            int buttonIndex = i;
            buttonDatas[buttonIndex].colour = Color.lightGray;
            buttonDatas[buttonIndex].letter = 0;
            buttonDatas[buttonIndex].value = 0;
            buttonDatas[buttonIndex].slot = -1;
            buttons[buttonIndex].gameObject.SetActive(false);
        }
        
        buttonUpdate();
        
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
