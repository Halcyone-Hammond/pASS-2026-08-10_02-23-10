using System.Collections.Generic;
using UnityEngine;

public class GameState
{
    public PieceState[] pieces;
    public int energy;
    //public int boardStage;
}
public class PieceState
{
    public bool ifPlaced;
    public int bx, bz, rot;
    public int[] theSlotLetters;                   //深拷贝
    public int[] theAttrValues;                    //深拷贝
    public int[] theAttrValues2;                   //第二重数值，深拷贝
    public int[] theSlotPerks;                     //有些特殊棋子，深拷贝
}


public class StateManager : MonoBehaviour
{
    List<GameState> undoStack = new();
    List<GameState> redoStack = new();
    const int MAX_HISTORY = 100;
    bool isRestoring = false;
    [SerializeField] Board theBoard;
    [SerializeField] SaultUI ui;

    void Start()
    {

    }

    public GameState Capture()
    {
        GameState currentState = new GameState();
        currentState.energy = ui.energy;
        for (int i = 0; i < theBoard.allPieces.Count; i++)
        {
            int index = i;
            Piece currentPiece = theBoard.allPieces[index];
            currentState.pieces[index] = new PieceState();
            currentState.pieces[index].ifPlaced = currentPiece.placed;
            currentState.pieces[index].bx = currentPiece.bx;
            currentState.pieces[index].bz = currentPiece.bz;
            currentState.pieces[index].rot = currentPiece.rot;
            for (int j = 0; j < currentPiece.slotLetters.Length; j++)
            {
                int javelin = j;
                currentState.pieces[index].theSlotLetters[javelin] = currentPiece.slotLetters[javelin];
                currentState.pieces[index].theAttrValues[javelin] = currentPiece.attrValues[javelin];
                currentState.pieces[index].theAttrValues2[javelin] = currentPiece.attrValues2[javelin];
                currentState.pieces[index].theSlotPerks[javelin] = currentPiece.slotPerks[javelin];
            }
        }
        return currentState;
    }
}
