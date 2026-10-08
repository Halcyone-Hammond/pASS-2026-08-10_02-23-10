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
    public string perkID;
    public string perkName;
    
    [TextArea]
    public string perkDescription;

    public PerkType perkType;

    public int perkOrder;           // 数字越小，优先级越高
}
