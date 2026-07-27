using System;
using NUnit.Framework;
using UnityEngine;

public sealed class OrientationTableTests
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    [Test]
    public void GeneratesExactly24UniqueStates()
    {
        Assert.That(OrientationTable.OrientationCount, Is.EqualTo(24));

        for (int first = 0; first < OrientationTable.OrientationCount; first++)
        {
            OrientationTable.OrientationState firstState = OrientationTable.GetState(first);
            for (int second = first + 1; second < OrientationTable.OrientationCount; second++)
            {
                OrientationTable.OrientationState secondState = OrientationTable.GetState(second);
                Assert.That(AreEqual(firstState, secondState), Is.False,
                    $"Orientations {first} and {second} are duplicates.");
            }
        }
    }

    [Test]
    public void EveryStateContainsEachPhysicalFaceExactlyOnce()
    {
        for (int orientation = 0; orientation < OrientationTable.OrientationCount; orientation++)
        {
            bool[] seen = new bool[6];
            OrientationTable.OrientationState state = OrientationTable.GetState(orientation);

            for (int slot = 0; slot < 6; slot++)
            {
                int faceId = (int)state.GetFaceInSlot(slot);
                Assert.That(faceId, Is.InRange(0, 5));
                Assert.That(seen[faceId], Is.False,
                    $"Orientation {orientation} repeats face ID {(DiceFaceId)faceId}.");
                seen[faceId] = true;
            }

            for (int face = 0; face < 6; face++)
            {
                Assert.That(seen[face], Is.True,
                    $"Orientation {orientation} is missing face ID {(DiceFaceId)face}.");
            }
        }
    }

    [Test]
    public void EveryTransitionResultIsAValidOrientation()
    {
        for (int orientation = 0; orientation < OrientationTable.OrientationCount; orientation++)
        {
            for (int direction = 0; direction < Directions.Length; direction++)
            {
                int next = OrientationTable.GetNextOrientation(orientation, Directions[direction]);
                Assert.That(next, Is.InRange(0, OrientationTable.OrientationCount - 1));
            }
        }
    }

    [TestCase(0, 1)]
    [TestCase(1, 0)]
    [TestCase(2, 3)]
    [TestCase(3, 2)]
    public void OppositeRollsRestoreEveryOrientation(int firstDirection, int secondDirection)
    {
        for (int orientation = 0; orientation < OrientationTable.OrientationCount; orientation++)
        {
            int next = OrientationTable.GetNextOrientation(orientation, Directions[firstDirection]);
            int restored = OrientationTable.GetNextOrientation(next, Directions[secondDirection]);
            Assert.That(restored, Is.EqualTo(orientation));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void FourRollsInOneDirectionRestoreEveryOrientation(int directionIndex)
    {
        for (int orientation = 0; orientation < OrientationTable.OrientationCount; orientation++)
        {
            int rolled = orientation;
            for (int roll = 0; roll < 4; roll++)
            {
                rolled = OrientationTable.GetNextOrientation(rolled, Directions[directionIndex]);
            }

            Assert.That(rolled, Is.EqualTo(orientation));
        }
    }

    [TestCase(0, DiceFaceId.InitialNorth)]
    [TestCase(1, DiceFaceId.InitialSouth)]
    [TestCase(2, DiceFaceId.InitialWest)]
    [TestCase(3, DiceFaceId.InitialEast)]
    public void IdentityRollProducesExpectedBottomFace(int directionIndex, DiceFaceId expectedBottom)
    {
        int next = OrientationTable.GetNextOrientation(
            OrientationTable.IdentityIndex, Directions[directionIndex]);

        Assert.That(OrientationTable.GetBottomFaceId(next), Is.EqualTo(expectedBottom));
    }

    [Test]
    public void GetIndexRoundTripsEveryState()
    {
        for (int orientation = 0; orientation < OrientationTable.OrientationCount; orientation++)
        {
            OrientationTable.OrientationState state = OrientationTable.GetState(orientation);
            Assert.That(OrientationTable.GetIndex(state.Top, state.North), Is.EqualTo(orientation));
        }
    }

    [Test]
    public void PhysicalFaceColorsAreCorrectAndOppositeIdsRemainDistinct()
    {
        Assert.That(OrientationTable.GetColor(DiceFaceId.InitialTop), Is.EqualTo(DiceColor.Red));
        Assert.That(OrientationTable.GetColor(DiceFaceId.InitialBottom), Is.EqualTo(DiceColor.Red));
        Assert.That(DiceFaceId.InitialTop, Is.Not.EqualTo(DiceFaceId.InitialBottom));

        Assert.That(OrientationTable.GetColor(DiceFaceId.InitialNorth), Is.EqualTo(DiceColor.Green));
        Assert.That(OrientationTable.GetColor(DiceFaceId.InitialSouth), Is.EqualTo(DiceColor.Green));
        Assert.That(DiceFaceId.InitialNorth, Is.Not.EqualTo(DiceFaceId.InitialSouth));

        Assert.That(OrientationTable.GetColor(DiceFaceId.InitialEast), Is.EqualTo(DiceColor.Blue));
        Assert.That(OrientationTable.GetColor(DiceFaceId.InitialWest), Is.EqualTo(DiceColor.Blue));
        Assert.That(DiceFaceId.InitialEast, Is.Not.EqualTo(DiceFaceId.InitialWest));
    }

    [Test]
    public void InvalidIndicesAndDirectionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrientationTable.GetState(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrientationTable.GetBottomFaceId(24));
        Assert.Throws<ArgumentException>(() =>
            OrientationTable.GetNextOrientation(OrientationTable.IdentityIndex, Vector2Int.zero));
    }

    [Test]
    public void DiceLogicSlotsMatchOrientationTableAcrossKnownSequence()
    {
        GameObject gameObject = new GameObject("DiceLogic_Orientation_Test");
        try
        {
            DiceLogic diceLogic = gameObject.AddComponent<DiceLogic>();
            diceLogic.topFace = CreateFace(DiceFaceId.InitialTop, DiceColor.Red);
            diceLogic.bottomFace = CreateFace(DiceFaceId.InitialBottom, DiceColor.Red);
            diceLogic.northFace = CreateFace(DiceFaceId.InitialNorth, DiceColor.Green);
            diceLogic.southFace = CreateFace(DiceFaceId.InitialSouth, DiceColor.Green);
            diceLogic.eastFace = CreateFace(DiceFaceId.InitialEast, DiceColor.Blue);
            diceLogic.westFace = CreateFace(DiceFaceId.InitialWest, DiceColor.Blue);

            Vector2Int[] sequence =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.left
            };

            int expectedOrientation = OrientationTable.IdentityIndex;
            Assert.That(diceLogic.GetCurrentOrientationIndex(), Is.EqualTo(expectedOrientation));

            for (int step = 0; step < sequence.Length; step++)
            {
                Vector2Int direction = sequence[step];
                diceLogic.UpdateFaces(direction, true);
                expectedOrientation =
                    OrientationTable.GetNextOrientation(expectedOrientation, direction);
                Assert.That(diceLogic.GetCurrentOrientationIndex(), Is.EqualTo(expectedOrientation),
                    $"Orientation mismatch after sequence step {step} ({direction}).");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static DiceFace CreateFace(DiceFaceId faceId, DiceColor color)
    {
        return new DiceFace(faceId.ToString(), color)
        {
            faceId = faceId
        };
    }

    private static bool AreEqual(
        OrientationTable.OrientationState first,
        OrientationTable.OrientationState second)
    {
        return first.Top == second.Top &&
               first.Bottom == second.Bottom &&
               first.North == second.North &&
               first.South == second.South &&
               first.East == second.East &&
               first.West == second.West;
    }
}
