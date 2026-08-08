using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SoftLockDetector
{
    private const int BoardSize = 7;
    private const int OrientationCount = 24;
    private const int FaceCount = 6;
    private const int NodeCount = BoardSize * BoardSize * OrientationCount;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private readonly int[] _bestCharge = new int[NodeCount * FaceCount];
    private readonly bool[] _reached = new bool[NodeCount];
    private readonly bool[] _inQueue = new bool[NodeCount];
    private readonly int[] _queue = new int[NodeCount];
    private readonly bool[] _occupied = new bool[BoardSize * BoardSize];

    private int _queueHead;
    private int _queueTail;
    private int _queueCount;

    public bool IsSoftLocked(
        Vector2Int playerPosition,
        int playerOrientation,
        int[] chargeByPhysicalFace,
        IReadOnlyList<SoftLockEnemySnapshot> enemies)
    {
        ValidatePlayerPosition(playerPosition);
        ValidateOrientation(playerOrientation);
        ValidateCharges(chargeByPhysicalFace);
        if (enemies == null)
        {
            throw new ArgumentNullException(nameof(enemies));
        }

        ResetWorkspace();
        int maxEnemyHP = BuildOccupancyAndValidateEnemies(playerPosition, enemies);
        if (enemies.Count == 0)
        {
            return false;
        }

        int startNode = EncodeNode(playerPosition.x, playerPosition.y, playerOrientation);
        int startChargeOffset = startNode * FaceCount;
        for (int face = 0; face < FaceCount; face++)
        {
            _bestCharge[startChargeOffset + face] =
                Mathf.Min(chargeByPhysicalFace[face], maxEnemyHP);
        }

        _reached[startNode] = true;
        Enqueue(startNode);
        PropagateMovement(maxEnemyHP);

        return !HasKillRoute(enemies);
    }

    private void ResetWorkspace()
    {
        for (int index = 0; index < _bestCharge.Length; index++)
        {
            _bestCharge[index] = -1;
        }

        Array.Clear(_reached, 0, _reached.Length);
        Array.Clear(_inQueue, 0, _inQueue.Length);
        Array.Clear(_occupied, 0, _occupied.Length);

        _queueHead = 0;
        _queueTail = 0;
        _queueCount = 0;
    }

    private int BuildOccupancyAndValidateEnemies(
        Vector2Int playerPosition,
        IReadOnlyList<SoftLockEnemySnapshot> enemies)
    {
        int maxEnemyHP = 0;

        for (int index = 0; index < enemies.Count; index++)
        {
            SoftLockEnemySnapshot enemy = enemies[index];
            if (!IsInsideBoard(enemy.Position))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemies),
                    enemy.Position,
                    $"Enemy at index {index} is outside the 7x7 board.");
            }

            if (enemy.CurrentHP <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemies),
                    enemy.CurrentHP,
                    $"Enemy at index {index} must have positive HP.");
            }

            int colorValue = (int)enemy.Color;
            if (colorValue < (int)DiceColor.Red || colorValue > (int)DiceColor.Blue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemies),
                    enemy.Color,
                    $"Enemy at index {index} has an invalid color.");
            }

            int cell = EncodeCell(enemy.Position.x, enemy.Position.y);
            if (_occupied[cell])
            {
                throw new ArgumentException(
                    $"Multiple enemies occupy {enemy.Position}.",
                    nameof(enemies));
            }

            if (enemy.Position == playerPosition)
            {
                throw new ArgumentException(
                    $"Player position {playerPosition} is occupied by an enemy.",
                    nameof(enemies));
            }

            _occupied[cell] = true;
            if (enemy.CurrentHP > maxEnemyHP)
            {
                maxEnemyHP = enemy.CurrentHP;
            }
        }

        return maxEnemyHP;
    }

    private void PropagateMovement(int maxEnemyHP)
    {
        while (_queueCount > 0)
        {
            int node = Dequeue();
            DecodeNode(node, out int x, out int y, out int orientation);
            int sourceChargeOffset = node * FaceCount;

            for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
            {
                Vector2Int direction = Directions[directionIndex];
                int nextX = x + direction.x;
                int nextY = y + direction.y;
                if (!IsInsideBoard(nextX, nextY) ||
                    _occupied[EncodeCell(nextX, nextY)])
                {
                    continue;
                }

                int nextOrientation =
                    OrientationTable.GetNextOrientation(orientation, direction);
                int nextNode = EncodeNode(nextX, nextY, nextOrientation);
                int destinationChargeOffset = nextNode * FaceCount;
                int bottomFace = (int)OrientationTable.GetBottomFaceId(nextOrientation);
                bool improved = false;

                for (int face = 0; face < FaceCount; face++)
                {
                    int candidateCharge = _bestCharge[sourceChargeOffset + face];
                    if (face == bottomFace && candidateCharge < maxEnemyHP)
                    {
                        candidateCharge++;
                    }

                    if (candidateCharge > _bestCharge[destinationChargeOffset + face])
                    {
                        _bestCharge[destinationChargeOffset + face] = candidateCharge;
                        improved = true;
                    }
                }

                if (!improved)
                {
                    continue;
                }

                _reached[nextNode] = true;
                if (!_inQueue[nextNode])
                {
                    Enqueue(nextNode);
                }
            }
        }
    }

    private bool HasKillRoute(IReadOnlyList<SoftLockEnemySnapshot> enemies)
    {
        for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
        {
            SoftLockEnemySnapshot enemy = enemies[enemyIndex];

            for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
            {
                Vector2Int attackDirection = Directions[directionIndex];
                int attackX = enemy.Position.x - attackDirection.x;
                int attackY = enemy.Position.y - attackDirection.y;
                if (!IsInsideBoard(attackX, attackY) ||
                    _occupied[EncodeCell(attackX, attackY)])
                {
                    continue;
                }

                for (int orientation = 0; orientation < OrientationCount; orientation++)
                {
                    int attackNode = EncodeNode(attackX, attackY, orientation);
                    if (!_reached[attackNode])
                    {
                        continue;
                    }

                    int nextOrientation =
                        OrientationTable.GetNextOrientation(orientation, attackDirection);
                    DiceFaceId bottomFace =
                        OrientationTable.GetBottomFaceId(nextOrientation);
                    if (OrientationTable.GetColor(bottomFace) != enemy.Color)
                    {
                        continue;
                    }

                    int charge =
                        _bestCharge[(attackNode * FaceCount) + (int)bottomFace];
                    if (charge >= enemy.CurrentHP)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void Enqueue(int node)
    {
        if (_queueCount >= NodeCount)
        {
            throw new InvalidOperationException(
                "[SoftLock] Work queue exceeded its fixed node capacity.");
        }

        _queue[_queueTail] = node;
        _queueTail++;
        if (_queueTail == NodeCount)
        {
            _queueTail = 0;
        }

        _queueCount++;
        _inQueue[node] = true;
    }

    private int Dequeue()
    {
        int node = _queue[_queueHead];
        _queueHead++;
        if (_queueHead == NodeCount)
        {
            _queueHead = 0;
        }

        _queueCount--;
        _inQueue[node] = false;
        return node;
    }

    private static int EncodeCell(int x, int y)
    {
        return (y * BoardSize) + x;
    }

    private static int EncodeNode(int x, int y, int orientation)
    {
        return (EncodeCell(x, y) * OrientationCount) + orientation;
    }

    private static void DecodeNode(
        int node,
        out int x,
        out int y,
        out int orientation)
    {
        orientation = node % OrientationCount;
        int cell = node / OrientationCount;
        x = cell % BoardSize;
        y = cell / BoardSize;
    }

    private static bool IsInsideBoard(Vector2Int position)
    {
        return IsInsideBoard(position.x, position.y);
    }

    private static bool IsInsideBoard(int x, int y)
    {
        return x >= 0 && x < BoardSize && y >= 0 && y < BoardSize;
    }

    private static void ValidatePlayerPosition(Vector2Int playerPosition)
    {
        if (!IsInsideBoard(playerPosition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(playerPosition),
                playerPosition,
                "Player position must be inside the 7x7 board.");
        }
    }

    private static void ValidateOrientation(int orientation)
    {
        if (orientation < 0 || orientation >= OrientationCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orientation),
                orientation,
                "Orientation must be in the range 0..23.");
        }
    }

    private static void ValidateCharges(int[] charges)
    {
        if (charges == null)
        {
            throw new ArgumentNullException(nameof(charges));
        }

        if (charges.Length < FaceCount)
        {
            throw new ArgumentException(
                "Charge array must contain at least six physical-face values.",
                nameof(charges));
        }

        for (int face = 0; face < FaceCount; face++)
        {
            if (charges[face] < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(charges),
                    charges[face],
                    $"Charge for physical face {(DiceFaceId)face} cannot be negative.");
            }
        }
    }
}
