using NUnit.Framework;

public sealed class SoftLockWarningStateTests
{
    private const int CheckInterval = 4;
    private const int WarningActions = 3;

    private SoftLockWarningState _state;

    [SetUp]
    public void SetUp()
    {
        _state = new SoftLockWarningState(CheckInterval, WarningActions);
    }

    [Test]
    public void NewlyDetectedSoftLockStartsWarningAtExactlyThreeActions()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true);

        ProcessNormalActions(CheckInterval, evaluator);

        Assert.That(_state.IsWarningActive, Is.True);
        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(3));
    }

    [Test]
    public void ActionThatStartsWarningDoesNotImmediatelyReduceIt()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true);

        ProcessNormalActions(CheckInterval, evaluator);

        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(3));
        Assert.That(evaluator.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void SubsequentSuccessfulNonKillActionReducesThreeToTwo()
    {
        SequenceEvaluator evaluator = StartWarning();

        ProcessSuccessfulAction(evaluator);

        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(2));
    }

    [Test]
    public void FailedMovementDoesNotReduceWarning()
    {
        SequenceEvaluator evaluator = StartWarning();

        _state.ProcessAction(
            successfulAction: false,
            successfulKill: false,
            waveChangedBoard: false,
            hardLocked: false,
            gameOverAlready: false,
            evaluateSoftLock: evaluator.Evaluate);

        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(3));
    }

    [Test]
    public void FailedCombatDoesNotReduceWarning()
    {
        SequenceEvaluator evaluator = StartWarning();

        _state.ProcessAction(
            successfulAction: false,
            successfulKill: false,
            waveChangedBoard: false,
            hardLocked: false,
            gameOverAlready: false,
            evaluateSoftLock: evaluator.Evaluate);

        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(3));
        Assert.That(evaluator.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void SuccessfulKillClearsWarningImmediately()
    {
        SequenceEvaluator evaluator = StartWarning();

        _state.ProcessAction(
            successfulAction: true,
            successfulKill: true,
            waveChangedBoard: false,
            hardLocked: false,
            gameOverAlready: false,
            evaluateSoftLock: evaluator.Evaluate);

        Assert.That(_state.IsWarningActive, Is.False);
        Assert.That(_state.WarningActionsRemaining, Is.Zero);
    }

    [Test]
    public void SuccessfulKillResetsPeriodicActionCounter()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(false);
        ProcessNormalActions(2, evaluator);
        Assert.That(_state.CheckActionCounter, Is.EqualTo(2));

        _state.ProcessAction(
            successfulAction: true,
            successfulKill: true,
            waveChangedBoard: false,
            hardLocked: false,
            gameOverAlready: false,
            evaluateSoftLock: evaluator.Evaluate);

        Assert.That(_state.CheckActionCounter, Is.Zero);
    }

    [Test]
    public void WarningReachingZeroPerformsFinalDetectorRecheck()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true, true);
        ProcessNormalActions(CheckInterval, evaluator);

        ProcessNormalActions(3, evaluator);

        Assert.That(evaluator.CallCount, Is.EqualTo(2));
    }

    [Test]
    public void FinalRecheckTrueTriggersSoftLockGameOver()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true, true);
        ProcessNormalActions(CheckInterval, evaluator);
        ProcessSuccessfulAction(evaluator);
        ProcessSuccessfulAction(evaluator);

        SoftLockActionOutcome outcome = ProcessSuccessfulAction(evaluator);

        Assert.That(outcome, Is.EqualTo(SoftLockActionOutcome.TriggerSoftLockGameOver));
    }

    [Test]
    public void FinalRecheckFalseClearsWarningWithoutGameOver()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true, false);
        ProcessNormalActions(CheckInterval, evaluator);
        ProcessSuccessfulAction(evaluator);
        ProcessSuccessfulAction(evaluator);

        SoftLockActionOutcome outcome = ProcessSuccessfulAction(evaluator);

        Assert.That(outcome, Is.EqualTo(SoftLockActionOutcome.None));
        Assert.That(_state.IsWarningActive, Is.False);
    }

    [Test]
    public void HardlockTakesPriorityOverSoftLock()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true);

        SoftLockActionOutcome outcome = _state.ProcessAction(
            successfulAction: true,
            successfulKill: false,
            waveChangedBoard: true,
            hardLocked: true,
            gameOverAlready: false,
            evaluateSoftLock: evaluator.Evaluate);

        Assert.That(outcome, Is.EqualTo(SoftLockActionOutcome.TriggerTrappedGameOver));
        Assert.That(evaluator.CallCount, Is.Zero);
    }

    [Test]
    public void ExistingMapFullGameOverIsNotOverwrittenBySoftLock()
    {
        SequenceEvaluator evaluator = StartWarning();

        SoftLockActionOutcome outcome = _state.ProcessAction(
            successfulAction: true,
            successfulKill: false,
            waveChangedBoard: true,
            hardLocked: false,
            gameOverAlready: true,
            evaluateSoftLock: evaluator.Evaluate);

        Assert.That(outcome, Is.EqualTo(SoftLockActionOutcome.None));
        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(3));
        Assert.That(evaluator.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void WaveSpawnCausesImmediateSoftLockEvaluation()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(false);

        _state.ProcessAction(
            successfulAction: true,
            successfulKill: false,
            waveChangedBoard: true,
            hardLocked: false,
            gameOverAlready: false,
            evaluateSoftLock: evaluator.Evaluate);

        Assert.That(evaluator.CallCount, Is.EqualTo(1));
        Assert.That(_state.CheckActionCounter, Is.Zero);
    }

    [Test]
    public void NewlySoftlockedWaveBoardStartsFullWarning()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true);

        ProcessWaveAction(evaluator);

        Assert.That(_state.IsWarningActive, Is.True);
        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(3));
    }

    [Test]
    public void WaveActionDoesNotDecrementNewWarning()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true);

        ProcessWaveAction(evaluator);

        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(3));
    }

    [Test]
    public void ExistingWarningClearsWhenWaveCreatesEscapeRoute()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true, false);
        ProcessNormalActions(CheckInterval, evaluator);

        ProcessWaveAction(evaluator);

        Assert.That(_state.IsWarningActive, Is.False);
    }

    [Test]
    public void ExistingWarningIsNotResetWhenWaveRemainsSoftlocked()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true, true);
        ProcessNormalActions(CheckInterval, evaluator);
        ProcessSuccessfulAction(evaluator);
        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(2));

        ProcessWaveAction(evaluator);

        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(1));
        Assert.That(_state.WarningActionsRemaining, Is.Not.EqualTo(3));
    }

    [Test]
    public void ExistingWarningDecrementsForActionThatTriggersWave()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true, true);
        ProcessNormalActions(CheckInterval, evaluator);

        ProcessWaveAction(evaluator);

        Assert.That(_state.WarningActionsRemaining, Is.EqualTo(2));
    }

    [Test]
    public void NoEnemiesResultCreatesNoWarning()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(false);

        ProcessNormalActions(CheckInterval, evaluator);

        Assert.That(_state.IsWarningActive, Is.False);
        Assert.That(_state.WarningActionsRemaining, Is.Zero);
    }

    [Test]
    public void RepeatedChecksDoNotRetainStaleEvaluationState()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true, false);
        ProcessNormalActions(CheckInterval, evaluator);
        Assert.That(_state.IsWarningActive, Is.True);

        _state.ClearAfterKill();
        ProcessWaveAction(evaluator);

        Assert.That(evaluator.CallCount, Is.EqualTo(2));
        Assert.That(_state.IsWarningActive, Is.False);
        Assert.That(_state.CheckActionCounter, Is.Zero);
    }

    private SequenceEvaluator StartWarning()
    {
        SequenceEvaluator evaluator = new SequenceEvaluator(true, true);
        ProcessNormalActions(CheckInterval, evaluator);
        return evaluator;
    }

    private void ProcessNormalActions(int count, SequenceEvaluator evaluator)
    {
        for (int action = 0; action < count; action++)
        {
            ProcessSuccessfulAction(evaluator);
        }
    }

    private SoftLockActionOutcome ProcessSuccessfulAction(
        SequenceEvaluator evaluator)
    {
        return _state.ProcessAction(
            successfulAction: true,
            successfulKill: false,
            waveChangedBoard: false,
            hardLocked: false,
            gameOverAlready: false,
            evaluateSoftLock: evaluator.Evaluate);
    }

    private SoftLockActionOutcome ProcessWaveAction(SequenceEvaluator evaluator)
    {
        return _state.ProcessAction(
            successfulAction: true,
            successfulKill: false,
            waveChangedBoard: true,
            hardLocked: false,
            gameOverAlready: false,
            evaluateSoftLock: evaluator.Evaluate);
    }

    private sealed class SequenceEvaluator
    {
        private readonly bool[] _results;
        private int _nextResult;

        public int CallCount { get; private set; }

        public SequenceEvaluator(params bool[] results)
        {
            _results = results;
        }

        public bool Evaluate()
        {
            if (_nextResult >= _results.Length)
            {
                Assert.Fail("Softlock evaluator was called more often than expected.");
            }

            CallCount++;
            return _results[_nextResult++];
        }
    }
}
