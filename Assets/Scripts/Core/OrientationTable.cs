using System;
using UnityEngine;

public static class OrientationTable
{
    public const int OrientationCount = 24;

    public readonly struct OrientationState
    {
        public DiceFaceId Top { get; }
        public DiceFaceId Bottom { get; }
        public DiceFaceId North { get; }
        public DiceFaceId South { get; }
        public DiceFaceId East { get; }
        public DiceFaceId West { get; }

        public OrientationState(
            DiceFaceId top,
            DiceFaceId bottom,
            DiceFaceId north,
            DiceFaceId south,
            DiceFaceId east,
            DiceFaceId west)
        {
            Top = top;
            Bottom = bottom;
            North = north;
            South = south;
            East = east;
            West = west;
        }

        public DiceFaceId GetFaceInSlot(int slot)
        {
            switch (slot)
            {
                case 0: return Top;
                case 1: return Bottom;
                case 2: return North;
                case 3: return South;
                case 4: return East;
                case 5: return West;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot must be in the range 0..5.");
            }
        }
    }

    private static readonly Vector2Int[] DiscoveryDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private static readonly OrientationState[] States = new OrientationState[OrientationCount];
    private static readonly int[,] Transitions = new int[OrientationCount, 4];
    private static readonly int[,] IndicesByTopAndNorth = new int[6, 6];

    public static int IdentityIndex { get; }

    static OrientationTable()
    {
        for (int top = 0; top < 6; top++)
        {
            for (int north = 0; north < 6; north++)
            {
                IndicesByTopAndNorth[top, north] = -1;
            }
        }

        States[0] = new OrientationState(
            DiceFaceId.InitialTop,
            DiceFaceId.InitialBottom,
            DiceFaceId.InitialNorth,
            DiceFaceId.InitialSouth,
            DiceFaceId.InitialEast,
            DiceFaceId.InitialWest);
        IdentityIndex = 0;

        int stateCount = 1;
        int readIndex = 0;

        while (readIndex < stateCount)
        {
            OrientationState current = States[readIndex];
            for (int directionIndex = 0; directionIndex < DiscoveryDirections.Length; directionIndex++)
            {
                OrientationState next = Roll(current, DiscoveryDirections[directionIndex]);
                if (FindStateIndex(next, stateCount) >= 0)
                {
                    continue;
                }

                if (stateCount >= OrientationCount)
                {
                    throw new InvalidOperationException(
                        "[Orientation] Generated more than 24 unique dice orientations.");
                }

                States[stateCount] = next;
                stateCount++;
            }

            readIndex++;
        }

        if (stateCount != OrientationCount)
        {
            throw new InvalidOperationException(
                $"[Orientation] Expected 24 unique dice orientations but generated {stateCount}.");
        }

        for (int orientation = 0; orientation < OrientationCount; orientation++)
        {
            OrientationState state = States[orientation];
            int top = (int)state.Top;
            int north = (int)state.North;
            if (IndicesByTopAndNorth[top, north] >= 0)
            {
                throw new InvalidOperationException(
                    $"[Orientation] Duplicate top/north pair: {state.Top}, {state.North}.");
            }

            IndicesByTopAndNorth[top, north] = orientation;

            for (int directionIndex = 0; directionIndex < DiscoveryDirections.Length; directionIndex++)
            {
                OrientationState next = Roll(state, DiscoveryDirections[directionIndex]);
                int nextIndex = FindStateIndex(next, OrientationCount);
                if (nextIndex < 0)
                {
                    throw new InvalidOperationException(
                        "[Orientation] A generated transition does not resolve to a known orientation.");
                }

                Transitions[orientation, directionIndex] = nextIndex;
            }
        }
    }

    public static int GetIndex(DiceFaceId top, DiceFaceId north)
    {
        ValidateFaceId(top, nameof(top));
        ValidateFaceId(north, nameof(north));

        int index = IndicesByTopAndNorth[(int)top, (int)north];
        if (index < 0)
        {
            throw new ArgumentException(
                $"The face pair top={top}, north={north} does not describe a legal dice orientation.");
        }

        return index;
    }

    public static int GetNextOrientation(int currentOrientation, Vector2Int direction)
    {
        ValidateOrientation(currentOrientation);
        int directionIndex = GetDirectionIndex(direction);
        return Transitions[currentOrientation, directionIndex];
    }

    public static DiceFaceId GetBottomFaceId(int orientation)
    {
        return GetState(orientation).Bottom;
    }

    public static DiceColor GetBottomColor(int orientation)
    {
        return GetColor(GetBottomFaceId(orientation));
    }

    public static DiceColor GetColor(DiceFaceId faceId)
    {
        ValidateFaceId(faceId, nameof(faceId));

        switch (faceId)
        {
            case DiceFaceId.InitialTop:
            case DiceFaceId.InitialBottom:
                return DiceColor.Red;
            case DiceFaceId.InitialNorth:
            case DiceFaceId.InitialSouth:
                return DiceColor.Green;
            case DiceFaceId.InitialEast:
            case DiceFaceId.InitialWest:
                return DiceColor.Blue;
            default:
                throw new ArgumentOutOfRangeException(nameof(faceId), faceId, "Unknown physical dice face ID.");
        }
    }

    public static OrientationState GetState(int orientation)
    {
        ValidateOrientation(orientation);
        return States[orientation];
    }

    private static OrientationState Roll(OrientationState state, Vector2Int direction)
    {
        if (direction == Vector2Int.up)
        {
            return new OrientationState(
                state.South, state.North, state.Top, state.Bottom, state.East, state.West);
        }

        if (direction == Vector2Int.down)
        {
            return new OrientationState(
                state.North, state.South, state.Bottom, state.Top, state.East, state.West);
        }

        if (direction == Vector2Int.left)
        {
            return new OrientationState(
                state.East, state.West, state.North, state.South, state.Bottom, state.Top);
        }

        if (direction == Vector2Int.right)
        {
            return new OrientationState(
                state.West, state.East, state.North, state.South, state.Top, state.Bottom);
        }

        throw new ArgumentException(
            $"Direction {direction} is not cardinal.", nameof(direction));
    }

    private static int FindStateIndex(OrientationState target, int count)
    {
        for (int index = 0; index < count; index++)
        {
            OrientationState candidate = States[index];
            if (candidate.Top == target.Top &&
                candidate.Bottom == target.Bottom &&
                candidate.North == target.North &&
                candidate.South == target.South &&
                candidate.East == target.East &&
                candidate.West == target.West)
            {
                return index;
            }
        }

        return -1;
    }

    private static int GetDirectionIndex(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return 0;
        if (direction == Vector2Int.down) return 1;
        if (direction == Vector2Int.left) return 2;
        if (direction == Vector2Int.right) return 3;

        throw new ArgumentException(
            $"Direction {direction} is not cardinal.", nameof(direction));
    }

    private static void ValidateOrientation(int orientation)
    {
        if (orientation < 0 || orientation >= OrientationCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orientation), orientation, "Orientation must be in the range 0..23.");
        }
    }

    private static void ValidateFaceId(DiceFaceId faceId, string parameterName)
    {
        int value = (int)faceId;
        if (value < 0 || value >= 6)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, faceId, "Physical face ID must be in the range 0..5.");
        }
    }
}
