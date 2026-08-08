using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SoftLockDetectorTests
{
    private SoftLockDetector _detector;

    [SetUp]
    public void SetUp()
    {
        _detector = new SoftLockDetector();
    }

    [Test]
    public void NoEnemies_ReturnsFalse()
    {
        bool result = _detector.IsSoftLocked(
            new Vector2Int(3, 3),
            OrientationTable.IdentityIndex,
            CreateCharges(),
            Array.Empty<SoftLockEnemySnapshot>());

        Assert.That(result, Is.False);
    }

    [Test]
    public void AdjacentKillableEnemy_ReturnsFalse()
    {
        Vector2Int direction = Vector2Int.up;
        DiceFaceId attackFace = GetAttackFace(OrientationTable.IdentityIndex, direction);
        int[] charges = CreateCharges();
        charges[(int)attackFace] = 1;
        SoftLockEnemySnapshot[] enemies =
        {
            new SoftLockEnemySnapshot(
                new Vector2Int(3, 4),
                OrientationTable.GetColor(attackFace),
                1)
        };

        bool result = _detector.IsSoftLocked(
            new Vector2Int(3, 3),
            OrientationTable.IdentityIndex,
            charges,
            enemies);

        Assert.That(result, Is.False);
    }

    [Test]
    public void CorrectColorButInsufficientChargeWithoutRoute_ReturnsTrue()
    {
        SoftLockEnemySnapshot[] enemies = CreateVerticalBlueWall();

        bool result = _detector.IsSoftLocked(
            new Vector2Int(0, 3),
            OrientationTable.IdentityIndex,
            CreateCharges(),
            enemies);

        Assert.That(result, Is.True);
    }

    [Test]
    public void SufficientChargeOnWrongColor_ReturnsTrue()
    {
        int[] charges = CreateCharges();
        DiceFaceId northAttackFace =
            GetAttackFace(OrientationTable.IdentityIndex, Vector2Int.up);
        DiceFaceId eastAttackFace =
            GetAttackFace(OrientationTable.IdentityIndex, Vector2Int.right);
        charges[(int)northAttackFace] = 10;
        charges[(int)eastAttackFace] = 10;

        SoftLockEnemySnapshot[] enemies =
        {
            new SoftLockEnemySnapshot(new Vector2Int(0, 1), DiceColor.Red, 1),
            new SoftLockEnemySnapshot(new Vector2Int(1, 0), DiceColor.Red, 1)
        };

        bool result = _detector.IsSoftLocked(
            Vector2Int.zero,
            OrientationTable.IdentityIndex,
            charges,
            enemies);

        Assert.That(result, Is.True);
    }

    [Test]
    public void SameColorPhysicalFacesRemainIndependent()
    {
        DiceFaceId lowChargeFace =
            GetAttackFace(OrientationTable.IdentityIndex, Vector2Int.up);
        Assert.That(lowChargeFace, Is.EqualTo(DiceFaceId.InitialNorth));

        int[] charges = CreateCharges();
        charges[(int)lowChargeFace] = 0;
        charges[(int)DiceFaceId.InitialSouth] = 10;
        SoftLockEnemySnapshot[] enemies =
        {
            new SoftLockEnemySnapshot(
                new Vector2Int(0, 1),
                OrientationTable.GetColor(lowChargeFace),
                1),
            new SoftLockEnemySnapshot(new Vector2Int(1, 0), DiceColor.Red, 1)
        };

        bool result = _detector.IsSoftLocked(
            Vector2Int.zero,
            OrientationTable.IdentityIndex,
            charges,
            enemies);

        Assert.That(
            OrientationTable.GetColor(DiceFaceId.InitialSouth),
            Is.EqualTo(OrientationTable.GetColor(lowChargeFace)));
        Assert.That(result, Is.True);
    }

    [Test]
    public void ReachableChargingPath_ReturnsFalse()
    {
        DiceFaceId attackFace =
            GetAttackFace(OrientationTable.IdentityIndex, Vector2Int.up);
        SoftLockEnemySnapshot[] enemies =
        {
            new SoftLockEnemySnapshot(
                new Vector2Int(3, 4),
                OrientationTable.GetColor(attackFace),
                1)
        };

        bool result = _detector.IsSoftLocked(
            new Vector2Int(3, 3),
            OrientationTable.IdentityIndex,
            CreateCharges(),
            enemies);

        Assert.That(result, Is.False);
    }

    [Test]
    public void EnemyCellsAreObstacles()
    {
        SoftLockEnemySnapshot[] closedWall = CreateVerticalBlueWall();
        SoftLockEnemySnapshot[] wallWithGap = CreateVerticalBlueWall(3);

        bool blockedResult = _detector.IsSoftLocked(
            new Vector2Int(0, 3),
            OrientationTable.IdentityIndex,
            CreateCharges(),
            closedWall);
        bool openResult = _detector.IsSoftLocked(
            new Vector2Int(0, 3),
            OrientationTable.IdentityIndex,
            CreateCharges(),
            wallWithGap);

        Assert.That(blockedResult, Is.True);
        Assert.That(openResult, Is.False);
    }

    [Test]
    public void MovementPossibleButNoKillRoute_ReturnsTrue()
    {
        Vector2Int playerPosition = new Vector2Int(0, 3);
        SoftLockEnemySnapshot[] enemies = CreateVerticalBlueWall();

        Assert.That(playerPosition + Vector2Int.up, Is.Not.EqualTo(enemies[0].Position));
        Assert.That(
            _detector.IsSoftLocked(
                playerPosition,
                OrientationTable.IdentityIndex,
                CreateCharges(),
                enemies),
            Is.True);
    }

    [Test]
    public void MaximumEnemyHPCapsChargeWithoutChangingResult()
    {
        const int enemyHP = 5;
        DiceFaceId attackFace =
            GetAttackFace(OrientationTable.IdentityIndex, Vector2Int.up);
        SoftLockEnemySnapshot[] enemies =
        {
            new SoftLockEnemySnapshot(
                new Vector2Int(3, 4),
                OrientationTable.GetColor(attackFace),
                enemyHP)
        };
        int[] cappedCharges = CreateCharges();
        int[] excessiveCharges = CreateCharges();
        cappedCharges[(int)attackFace] = enemyHP;
        excessiveCharges[(int)attackFace] = 100;

        bool cappedResult = _detector.IsSoftLocked(
            new Vector2Int(3, 3),
            OrientationTable.IdentityIndex,
            cappedCharges,
            enemies);
        bool excessiveResult = _detector.IsSoftLocked(
            new Vector2Int(3, 3),
            OrientationTable.IdentityIndex,
            excessiveCharges,
            enemies);

        Assert.That(cappedResult, Is.False);
        Assert.That(excessiveResult, Is.EqualTo(cappedResult));
    }

    [Test]
    public void RepeatedCallsDoNotLeakPreviousState()
    {
        DiceFaceId attackFace =
            GetAttackFace(OrientationTable.IdentityIndex, Vector2Int.up);
        int[] killableCharges = CreateCharges();
        killableCharges[(int)attackFace] = 1;
        SoftLockEnemySnapshot[] killableEnemy =
        {
            new SoftLockEnemySnapshot(
                new Vector2Int(3, 4),
                OrientationTable.GetColor(attackFace),
                1)
        };

        bool first = _detector.IsSoftLocked(
            new Vector2Int(3, 3),
            OrientationTable.IdentityIndex,
            killableCharges,
            killableEnemy);
        bool second = _detector.IsSoftLocked(
            new Vector2Int(0, 3),
            OrientationTable.IdentityIndex,
            CreateCharges(),
            CreateVerticalBlueWall());
        bool third = _detector.IsSoftLocked(
            new Vector2Int(3, 3),
            OrientationTable.IdentityIndex,
            killableCharges,
            killableEnemy);

        Assert.That(first, Is.False);
        Assert.That(second, Is.True);
        Assert.That(third, Is.False);
    }

    [Test]
    public void InputChargeArrayIsNotMutated()
    {
        int[] charges = { 0, 1, 2, 3, 4, 5 };
        int[] original = (int[])charges.Clone();

        _detector.IsSoftLocked(
            new Vector2Int(0, 3),
            OrientationTable.IdentityIndex,
            charges,
            CreateVerticalBlueWall());

        Assert.That(charges, Is.EqualTo(original));
    }

    [Test]
    public void EnemyCollectionIsNotMutated()
    {
        List<SoftLockEnemySnapshot> enemies =
            new List<SoftLockEnemySnapshot>(CreateVerticalBlueWall());
        int originalCount = enemies.Count;
        SoftLockEnemySnapshot firstEnemy = enemies[0];

        _detector.IsSoftLocked(
            new Vector2Int(0, 3),
            OrientationTable.IdentityIndex,
            CreateCharges(),
            enemies);

        Assert.That(enemies.Count, Is.EqualTo(originalCount));
        Assert.That(enemies[0].Position, Is.EqualTo(firstEnemy.Position));
        Assert.That(enemies[0].Color, Is.EqualTo(firstEnemy.Color));
        Assert.That(enemies[0].CurrentHP, Is.EqualTo(firstEnemy.CurrentHP));
    }

    [Test]
    public void InvalidPlayerPositionThrowsUsefulArgumentException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _detector.IsSoftLocked(
                new Vector2Int(-1, 0),
                OrientationTable.IdentityIndex,
                CreateCharges(),
                Array.Empty<SoftLockEnemySnapshot>()));
    }

    [Test]
    public void InvalidOrientationThrowsUsefulArgumentException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _detector.IsSoftLocked(
                Vector2Int.zero,
                OrientationTable.OrientationCount,
                CreateCharges(),
                Array.Empty<SoftLockEnemySnapshot>()));
    }

    [Test]
    public void ChargeArrayShorterThanSixThrowsUsefulArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _detector.IsSoftLocked(
                Vector2Int.zero,
                OrientationTable.IdentityIndex,
                new int[5],
                Array.Empty<SoftLockEnemySnapshot>()));
    }

    [Test]
    public void EnemyWithNonPositiveHPIsRejected()
    {
        SoftLockEnemySnapshot[] enemies =
        {
            new SoftLockEnemySnapshot(new Vector2Int(1, 1), DiceColor.Red, 0)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _detector.IsSoftLocked(
                Vector2Int.zero,
                OrientationTable.IdentityIndex,
                CreateCharges(),
                enemies));
    }

    [Test]
    public void DuplicateEnemyPositionsAreRejected()
    {
        SoftLockEnemySnapshot[] enemies =
        {
            new SoftLockEnemySnapshot(new Vector2Int(1, 1), DiceColor.Red, 1),
            new SoftLockEnemySnapshot(new Vector2Int(1, 1), DiceColor.Blue, 2)
        };

        Assert.Throws<ArgumentException>(() =>
            _detector.IsSoftLocked(
                Vector2Int.zero,
                OrientationTable.IdentityIndex,
                CreateCharges(),
                enemies));
    }

    [Test]
    public void PlayerPositionOccupiedByEnemyIsRejected()
    {
        SoftLockEnemySnapshot[] enemies =
        {
            new SoftLockEnemySnapshot(Vector2Int.zero, DiceColor.Red, 1)
        };

        Assert.Throws<ArgumentException>(() =>
            _detector.IsSoftLocked(
                Vector2Int.zero,
                OrientationTable.IdentityIndex,
                CreateCharges(),
                enemies));
    }

    private static int[] CreateCharges()
    {
        return new int[6];
    }

    private static DiceFaceId GetAttackFace(int orientation, Vector2Int direction)
    {
        int nextOrientation =
            OrientationTable.GetNextOrientation(orientation, direction);
        return OrientationTable.GetBottomFaceId(nextOrientation);
    }

    private static SoftLockEnemySnapshot[] CreateVerticalBlueWall(int gapY = -1)
    {
        int count = gapY >= 0 ? 6 : 7;
        SoftLockEnemySnapshot[] enemies = new SoftLockEnemySnapshot[count];
        int index = 0;

        for (int y = 0; y < 7; y++)
        {
            if (y == gapY)
            {
                continue;
            }

            enemies[index] =
                new SoftLockEnemySnapshot(new Vector2Int(1, y), DiceColor.Blue, 1);
            index++;
        }

        return enemies;
    }
}
