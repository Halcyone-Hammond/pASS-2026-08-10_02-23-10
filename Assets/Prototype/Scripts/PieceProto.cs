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
    //将Perk放到Cell中，每个Cell可以有多个Perk，最大化可能性。如果要以Piece做单位，初始化时就统一赋值。
    public int slotID;
    public Vector2Int offset;                   //相对于Piece的偏移量
    public CellAttribute attribute;             //代表的属性类型，S、A、U、L
    public int iniValue = 0;                    //初始值，默认为0

    public List<CellPerkData> perkDatas = new List<CellPerkData>();
}

[CreateAssetMenu(fileName = "PieceProto", menuName = "Scriptable Objects/PieceProto")]
public class PieceProto : ScriptableObject
{
    //本ScriptableObject用于定义一个Piece的基本信息，包括ID、名称、旋转中心点、以及组成该Piece的Cell信息。
    public string pieceName;
    public string pieceID;

    public Vector2Int pivot = Vector2Int.zero;

    public List<CellDefinition> cells = new List<CellDefinition>();
}
