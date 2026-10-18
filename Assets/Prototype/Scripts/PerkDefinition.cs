using UnityEngine;

public enum PerkType
{
    AddValue,
    ShareIncrease,
    multiFaces
}

[CreateAssetMenu(fileName = "PerkDefinition", menuName = "Scriptable Objects/PerkDefinition")]
public class PerkDefinition : ScriptableObject
{
    //本ScriptableObject用于定义一个Perk的基本信息，包括ID、名称、描述、类型和优先级顺序。
    public string perkID;
    public string perkName;
    
    [TextArea]
    public string perkDescription;

    public PerkType perkType;

    public int perkOrder;           // 数字越小，优先级越高
}
