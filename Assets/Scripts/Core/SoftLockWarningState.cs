using System;

public enum SoftLockActionOutcome
{
    None,
    TriggerTrappedGameOver,
    TriggerSoftLockGameOver
}

public sealed class SoftLockWarningState
{
    private int _checkInterval;
    private int _warningMaxActions;

    public bool IsWarningActive { get; private set; }
    public int WarningActionsRemaining { get; private set; }
    public int CheckActionCounter { get; private set; }

    public SoftLockWarningState(int checkInterval, int warningMaxActions)
    {
        Configure(checkInterval, warningMaxActions);
    }

    public void Configure(int checkInterval, int warningMaxActions)
    {
        if (checkInterval < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkInterval), checkInterval, "Check interval must be at least 1.");
        }

        if (warningMaxActions < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(warningMaxActions), warningMaxActions, "Warning actions must be at least 1.");
        }

        _checkInterval = checkInterval;
        _warningMaxActions = warningMaxActions;
        ClearWarningAndCounter();
    }

    public void ClearAfterKill()
    {
        ClearWarningAndCounter();
    }

    public SoftLockActionOutcome ProcessAction(
        bool successfulAction,
        bool successfulKill,
        bool waveChangedBoard,
        bool hardLocked,
        bool gameOverAlready,
        Func<bool> evaluateSoftLock)
    {
        if (!successfulAction)
        {
            return SoftLockActionOutcome.None;
        }

        if (successfulKill)
        {
            ClearAfterKill();
        }

        if (gameOverAlready)
        {
            return SoftLockActionOutcome.None;
        }

        if (hardLocked)
        {
            return SoftLockActionOutcome.TriggerTrappedGameOver;
        }

        if (successfulKill)
        {
            return SoftLockActionOutcome.None;
        }

        bool warningWasActiveAtActionStart = IsWarningActive;

        if (waveChangedBoard)
        {
            CheckActionCounter = 0;
            ResolveCheck(Evaluate(evaluateSoftLock), preserveExistingWarning: true);
        }
        else if (!warningWasActiveAtActionStart)
        {
            CheckActionCounter++;
            if (CheckActionCounter >= _checkInterval)
            {
                CheckActionCounter = 0;
                ResolveCheck(Evaluate(evaluateSoftLock), preserveExistingWarning: false);
            }
        }

        if (!warningWasActiveAtActionStart || !IsWarningActive)
        {
            return SoftLockActionOutcome.None;
        }

        WarningActionsRemaining--;
        if (WarningActionsRemaining > 0)
        {
            return SoftLockActionOutcome.None;
        }

        if (Evaluate(evaluateSoftLock))
        {
            return SoftLockActionOutcome.TriggerSoftLockGameOver;
        }

        ClearWarningAndCounter();
        return SoftLockActionOutcome.None;
    }

    private void ResolveCheck(bool softLocked, bool preserveExistingWarning)
    {
        if (!softLocked)
        {
            ClearWarningAndCounter();
            return;
        }

        if (preserveExistingWarning && IsWarningActive)
        {
            return;
        }

        IsWarningActive = true;
        WarningActionsRemaining = _warningMaxActions;
    }

    private void ClearWarningAndCounter()
    {
        IsWarningActive = false;
        WarningActionsRemaining = 0;
        CheckActionCounter = 0;
    }

    private static bool Evaluate(Func<bool> evaluateSoftLock)
    {
        if (evaluateSoftLock == null)
        {
            throw new ArgumentNullException(nameof(evaluateSoftLock));
        }

        return evaluateSoftLock();
    }
}
