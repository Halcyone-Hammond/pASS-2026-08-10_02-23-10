using UnityEngine;
using System.Collections.Generic;
using System;

public enum CellAttribute
{
    S,
    A,
    U,
    L
}

[Serializable] public class CellPerkData
{
    public PerkDefinition perk;

    public List<int> targetSlotIds = new List<int>();

    public int theAmount = 1;
}

[Serializable] public class CellDefinition
{
    public int slotID;
    public Vector2Int offset;
    public CellAttribute attribute;
    public int iniValue = 0;
    
    public List<CellPerkData> perkDatas = new List<CellPerkData>();
}

[CreateAssetMenu(fileName = "PieceProto", menuName = "Scriptable Objects/PieceProto")]
public class PieceProto : ScriptableObject
{
    public string pieceName;
    public string pieceID;

    public Vector2Int pivot = Vector2Int.zero;

    public List<CellDefinition> cells = new List<CellDefinition>();
}
