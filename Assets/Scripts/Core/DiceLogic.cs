using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum DiceColor { Red, Green, Blue }

[System.Serializable]
public class DiceFace
{
    public string name;
    public DiceColor color;
    public DiceFaceId faceId;
    public int charge;

    public TextMeshPro textMesh; 

    public DiceFace(string name, DiceColor color)
    {
        this.name = name;
        this.color = color;
        this.charge = 0;
    }

    public void AddCharge(int amount) => charge += amount;
    public void ResetCharge() => charge = 0;

    // [추가됨] 텍스트 업데이트 함수
    public void UpdateText()
    {
        if (textMesh != null)
        {
            textMesh.text = charge.ToString();
        }
    }
}

public class DiceLogic : MonoBehaviour
{
    [Header("Dice Data (Connect Text Here)")]
    public DiceFace topFace;
    public DiceFace bottomFace;
    public DiceFace northFace;
    public DiceFace southFace;
    public DiceFace eastFace;
    public DiceFace westFace;

    [System.Serializable]
    public struct UISlot { public Image bgImage; public TextMeshProUGUI text; }

    [Header("UI References (Bottom HUD)")]
    public UISlot ui_Center;
    public UISlot ui_North;
    public UISlot ui_South;
    public UISlot ui_East;
    public UISlot ui_West;

    private Color colorRed = new Color(1f, 0.2f, 0.2f);
    private Color colorGreen = new Color(0.2f, 1f, 0.2f);
    private Color colorBlue = new Color(0.2f, 0.6f, 1f);

    void Start() 
    { 
        InitializeDice(); 
        UpdateUI(); // 시작 시 텍스트 갱신
    }

    void InitializeDice()
    {
        topFace.name = "Top";
        topFace.color = DiceColor.Red;
        topFace.faceId = DiceFaceId.InitialTop;

        bottomFace.name = "Bottom";
        bottomFace.color = DiceColor.Red;
        bottomFace.faceId = DiceFaceId.InitialBottom;
        
        northFace.name = "North";
        northFace.color = DiceColor.Green;
        northFace.faceId = DiceFaceId.InitialNorth;

        southFace.name = "South";
        southFace.color = DiceColor.Green;
        southFace.faceId = DiceFaceId.InitialSouth;
        
        eastFace.name = "East";
        eastFace.color = DiceColor.Blue;
        eastFace.faceId = DiceFaceId.InitialEast;

        westFace.name = "West";
        westFace.color = DiceColor.Blue;
        westFace.faceId = DiceFaceId.InitialWest;
    }

    public void UpdateFaces(Vector2Int direction, bool isCombat)
    {
        DiceFace temp;

        if (direction == Vector2Int.up) // North
        {
            temp = topFace; topFace = southFace; southFace = bottomFace; bottomFace = northFace; northFace = temp;
        }
        else if (direction == Vector2Int.down) // South
        {
            temp = topFace; topFace = northFace; northFace = bottomFace; bottomFace = southFace; southFace = temp;
        }
        else if (direction == Vector2Int.right) // East
        {
            temp = topFace; topFace = westFace; westFace = bottomFace; bottomFace = eastFace; eastFace = temp;
        }
        else if (direction == Vector2Int.left) // West
        {
            temp = topFace; topFace = eastFace; eastFace = bottomFace; bottomFace = westFace; westFace = temp;
        }

        if (!isCombat)
        {
            bottomFace.AddCharge(1);
        }
        
        UpdateUI();
    }

    public void ConsumeCharge(Vector2Int direction, int amount)
    {
        DiceFace targetFace = GetFutureBottomFace(direction);
        targetFace.charge -= amount;
        if (targetFace.charge < 0) targetFace.charge = 0;
        UpdateUI();
    }

    public void ResetBottomCharge()
    {
        bottomFace.ResetCharge();
        UpdateUI();
    }

    void UpdateUI()
    {
        // 1. 하단 UI 슬롯 업데이트
        SetSlot(ui_Center, bottomFace);
        SetSlot(ui_North, northFace);
        SetSlot(ui_South, southFace);
        SetSlot(ui_East, eastFace);
        SetSlot(ui_West, westFace);

        // 2. [추가됨] 3D 플레이어 텍스트 업데이트
        topFace.UpdateText();
        bottomFace.UpdateText();
        northFace.UpdateText();
        southFace.UpdateText();
        eastFace.UpdateText();
        westFace.UpdateText();
    }

    void SetSlot(UISlot slot, DiceFace face)
    {
        if (slot.bgImage == null || slot.text == null) return;
        slot.text.text = face.charge.ToString();
        switch (face.color)
        {
            case DiceColor.Red: slot.bgImage.color = colorRed; break;
            case DiceColor.Green: slot.bgImage.color = colorGreen; break;
            case DiceColor.Blue: slot.bgImage.color = colorBlue; break;
        }
    }

    public DiceFace GetFutureBottomFace(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return northFace;
        if (direction == Vector2Int.down) return southFace;
        if (direction == Vector2Int.right) return eastFace;
        if (direction == Vector2Int.left) return westFace;
        return null; 
    }

    public int GetCurrentOrientationIndex()
    {
        return OrientationTable.GetIndex(topFace.faceId, northFace.faceId);
    }

    public int GetCharge(DiceFaceId faceId)
    {
        if (topFace.faceId == faceId) return topFace.charge;
        if (bottomFace.faceId == faceId) return bottomFace.charge;
        if (northFace.faceId == faceId) return northFace.charge;
        if (southFace.faceId == faceId) return southFace.charge;
        if (eastFace.faceId == faceId) return eastFace.charge;
        if (westFace.faceId == faceId) return westFace.charge;

        throw new System.ArgumentOutOfRangeException(
            nameof(faceId), faceId, "No current dice face has the requested physical face ID.");
    }
}
