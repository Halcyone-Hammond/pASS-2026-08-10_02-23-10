using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

    GameState tempState;
    bool dragging = false;

    void Start()
    {
        ui.undoBtn.interactable = false;
        ui.redoBtn.interactable = false;
    }

    //State状态记录
    public GameState Capture()
    {
        GameState currentState = new GameState();
        currentState.pieces = new PieceState[theBoard.allPieces.Count];
        currentState.energy = ui.energy;
        for (int i = 0; i < theBoard.allPieces.Count; i++)                                                      //这里Count是不是也要记状态呢？局内如果复制粘贴棋子呢
        {
            int index = i;
            Piece currentPiece = theBoard.allPieces[index];
            currentState.pieces[index] = new PieceState();
            currentState.pieces[index].ifPlaced = currentPiece.placed;
            currentState.pieces[index].bx = currentPiece.bx;
            currentState.pieces[index].bz = currentPiece.bz;
            currentState.pieces[index].rot = currentPiece.rot;

            currentState.pieces[index].theSlotLetters= (int[])currentPiece.slotLetters.Clone();
            currentState.pieces[index].theAttrValues = (int[])currentPiece.attrValues.Clone();
            currentState.pieces[index].theAttrValues2 = (int[])currentPiece.attrValues2.Clone();
            currentState.pieces[index].theSlotPerks = (int[])currentPiece.slotPerks.Clone();
        }
        return currentState;
    }

    //将Redo和Undo所记状态付诸现实
    public void Apply(GameState goldenState)
    {
        isRestoring = true;

        ui.energy = goldenState.energy;
        theBoard.ClearBoardCells();
        for (int i = 0; i < theBoard.allPieces.Count; i++)                                                      //这里Count是不是也要记状态呢？局内如果复制粘贴棋子呢
        {
            int index = i;
            Piece currentPiece = theBoard.allPieces[index];

            currentPiece.placed = goldenState.pieces[index].ifPlaced;
            currentPiece.bx = goldenState.pieces[index].bx;
            currentPiece.bz = goldenState.pieces[index].bz;
            currentPiece.rot = goldenState.pieces[index].rot;
            if(currentPiece.placed)
            {
                currentPiece.SetPlacedTransform(currentPiece.bx, currentPiece.bz, currentPiece.rot);
                Vector2Int[] cells = theBoard.GetCells(currentPiece, currentPiece.bx, currentPiece.bz, currentPiece.rot);                                   //用棋子记录的新位置算出格子
                for (int j = 0; j < cells.Length; j++)
                {
                    theBoard.board[cells[j].x, cells[j].y].piece = currentPiece; // 记录"这格被棋子 p 占据"
                    theBoard.board[cells[j].x, cells[j].y].slot = j;  // 记录"是 p 的第 i 个槽位"
                }
            }
            else
            {
                currentPiece.GoToTray();
            }

            currentPiece.slotLetters = (int[])goldenState.pieces[index].theSlotLetters.Clone();
            currentPiece.attrValues = (int[])goldenState.pieces[index].theAttrValues.Clone();
            currentPiece.attrValues2 = (int[])goldenState.pieces[index].theAttrValues2.Clone();
            currentPiece.slotPerks = (int[])goldenState.pieces[index].theSlotPerks.Clone();

            currentPiece.RefreshVisuals();

        }

        ui.RefreshAll();

        isRestoring = false;
    }

    public void ArchiveState(GameState platinumState)
    {
        //一段操作阶段的最终状态归档（暂时不做）
    }

    public void EnterNextStage()
    {
        //进入下一个阶段（还没想好）
    }

    //在做了一个有效变化之后，将Redo消掉，Undo栈新记录一个状态——不再直接调用，由PushTemp过滤。
    public void Record()
    {
        if (isRestoring) return;
        undoStack.Add(Capture());
        
        if (undoStack.Count> MAX_HISTORY)
        {
            undoStack.RemoveAt(0);
        }
        redoStack.Clear();

        ui.undoBtn.interactable = true;
        ui.redoBtn.interactable = false;
    }

    //Undo一次，将现状态推到Redo栈内，将Undo栈读取一位并消去
    public void Undo()
    {
        if (undoStack.Count == 0) return;
        redoStack.Add(Capture());
        Apply(undoStack[^1]);
        undoStack.RemoveAt(undoStack.Count - 1);

        ui.redoBtn.interactable = true;
        if (undoStack.Count == 0)   ui.undoBtn.interactable = false;
    }

    //Redo一次，将先状态推回Undo栈内，将Redo栈读取一位并消去
    public void Redo()
    {
        if(redoStack.Count == 0) return;
        undoStack.Add(Capture());
        Apply(redoStack[^1]);
        redoStack.RemoveAt(redoStack.Count - 1);

        ui.undoBtn.interactable = true;
        if (redoStack.Count == 0)    ui.redoBtn.interactable = false;
    }

    public void Pending(bool dragState)
    {
        dragging = dragState;
        tempState = Capture();
    }

    bool SameState(GameState a, GameState b)
    {
        bool judging = true;
        if (a.energy != b.energy) return false;
        //if (a.pieces.Length != b.pieces.Length) return false;             //目前没有这个情况，这个在操作阶段外

        for(int i=0;i<a.pieces.Length; i++)
        {
            PieceState aa = a.pieces[i];
            PieceState bb = b.pieces[i];

            if (aa.ifPlaced != bb.ifPlaced) return false;
            if (aa.rot != bb.rot) return false;
            if (aa.ifPlaced && (aa.bx != bb.bx || aa.bz != bb.bz)) return false;

            //目前没看到会仅给其他数值带来不变的改动
        }
        return judging;
    }

    //将处理两个不同的回撤——拖动失败回撤和两状态一致回撤。
    public void PushTemp()
    {
        if (isRestoring) return;

        if (dragging)
        {
            GameState checkState = Capture();
            bool sameOrNot = SameState(tempState, checkState);
            Debug.Log(sameOrNot);
            if (sameOrNot)
            {
                CancelPending();
                return;
            }
        }

        undoStack.Add(tempState);
        if (undoStack.Count > MAX_HISTORY)
        {
            undoStack.RemoveAt(0);
        }
        redoStack.Clear();
        CancelPending();

        ui.undoBtn.interactable = true;
        ui.redoBtn.interactable = false;

    }

    public void CancelPending()
    {
        dragging = false;
        tempState = null;
    }

    //买定离手环节，一段操作阶段的结束，清空双栈
    public void Submit()
    {
        undoStack.Clear();
        redoStack.Clear();
        ArchiveState(Capture());
        EnterNextStage();
    }
}
