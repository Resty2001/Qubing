using UnityEngine;
using TMPro;

public class Enemy : MonoBehaviour
{
    public int maxHP;
    public int currentHP;
    public DiceColor myColor;
    
    // [추가됨] 적이 위치한 그리드 좌표
    public Vector2Int gridPos;

    [Header("Visual References")]
    public TextMeshPro hpText;
    public Renderer meshRenderer;

    [Header("Materials")]
    public Material matRed;
    public Material matGreen;
    public Material matBlue;

    // 초기화 할 때 좌표(pos)도 같이 받도록 수정
    public void Initialize(int hp, DiceColor color, Vector2Int pos)
    {
        maxHP = hp;
        currentHP = hp;
        myColor = color;
        gridPos = pos; // 좌표 저장

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (hpText != null) hpText.text = currentHP.ToString();
        if (meshRenderer != null)
        {
            switch (myColor)
            {
                case DiceColor.Red: meshRenderer.sharedMaterial = matRed; break;
                case DiceColor.Green: meshRenderer.sharedMaterial = matGreen; break;
                case DiceColor.Blue: meshRenderer.sharedMaterial = matBlue; break;
            }
        }
    }
}
