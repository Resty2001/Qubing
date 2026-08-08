using UnityEngine;

public readonly struct SoftLockEnemySnapshot
{
    public Vector2Int Position { get; }
    public DiceColor Color { get; }
    public int CurrentHP { get; }

    public SoftLockEnemySnapshot(
        Vector2Int position,
        DiceColor color,
        int currentHP)
    {
        Position = position;
        Color = color;
        CurrentHP = currentHP;
    }
}
