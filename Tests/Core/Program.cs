using System;
using System.Collections.Generic;
using HotCell.Core;

internal static class Program
{
    private static int passed;
    private static int failed;
    private static readonly int[] Star = { 0, 4, 2, 6, 1, 5, 3, 7 };

    private static int Main()
    {
        Run("dose stages switch at all exact boundaries", DoseBoundaries);
        Run("inverse square, shielding and source radius", RadiationRate);
        Run("dose accumulation is monotonic and saturates safely", DoseAccumulation);
        Run("invalid physical numbers are rejected", InvalidRadiation);
        Run("traitor count has exact probability boundaries", RoleBoundaries);
        Run("two traitors are unique for every supported crew size", UniqueRoles);
        Run("role input and null random validation", InvalidRoles);
        Run("three survivors include secret roles in a four-suit shift", FourSuitTwoTraitors);
        Run("crew success excludes every traitor winner", CrewWinsExclusively);
        Run("crew needs delivery, containment and unexpired time", FailureTriggers);
        Run("a living ejected traitor remains eligible", EjectedTraitor);
        Run("dose ceiling, incapacitation and extraction disqualify traitors", IneligibleTraitors);
        Run("zero traitors and mutual failure produce nobody wins", NobodyWins);
        Run("fewer than three qualifying evacuees fails", InsufficientSurvivors);
        Run("incomplete work cannot be resolved as a victory", UnresolvedShift);
        Run("snapshot and outcome validation", InvalidOutcomes);
        Run("a star sequence within tolerance seals", ValidTorque);
        Run("wrong order is accepted but invalid until full reset", WrongTorqueOrder);
        Run("under torque, over torque and corrective adjustments", TorqueTolerance);
        Run("loosening a seated bolt requires full reset", TorqueLoosening);
        Run("torque clamps physical values and rejects invalid inputs", InvalidTorque);
        Run("lockout requires six uninterrupted seconds", CompleteLockout);
        Run("release or disconnection cancels without consuming a use", InterruptedLockout);
        Run("changing the actor or target resets a hold", ChangedLockout);
        Run("two completed lockouts exhaust the shift allowance", ExhaustedLockout);
        Run("lockout rejects self targets and invalid inputs", InvalidLockout);
        Console.WriteLine("\n" + passed + " passed; " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void DoseBoundaries()
    {
        Equal(DoseStage.Clear, RadiationRules.Stage(0f, 100f));
        Equal(DoseStage.Clear, RadiationRules.Stage(0f, float.Epsilon));
        Equal(DoseStage.Clear, RadiationRules.Stage(24.999f, 100f));
        Equal(DoseStage.Noise, RadiationRules.Stage(25f, 100f));
        Equal(DoseStage.Noise, RadiationRules.Stage(49.999f, 100f));
        Equal(DoseStage.Tremor, RadiationRules.Stage(50f, 100f));
        Equal(DoseStage.Tremor, RadiationRules.Stage(74.999f, 100f));
        Equal(DoseStage.Failing, RadiationRules.Stage(75f, 100f));
        Equal(DoseStage.Failing, RadiationRules.Stage(94.999f, 100f));
        Equal(DoseStage.Collapsed, RadiationRules.Stage(95f, 100f));
        Equal(DoseStage.Collapsed, RadiationRules.Stage(0.95f, 1f));
        Equal(DoseStage.Failing, RadiationRules.Stage(0.9499999f, 1f));
        Equal(DoseStage.Collapsed, RadiationRules.Stage(float.MaxValue, 100f));
        Equal(DoseStage.Tremor, RadiationRules.Stage(100f, 200f));
    }

    private static void RadiationRate()
    {
        Near(10f, RadiationRules.Rate(10f, 1f, 1f));
        Near(2.5f, RadiationRules.Rate(10f, 4f, 1f));
        Near(0.25f, RadiationRules.Rate(10f, 4f, 0.1f));
        Near(40f, RadiationRules.Rate(10f, 0f, 1f));
        Near(40f, RadiationRules.Rate(10f, 0.1f, 1f));
        Near(0f, RadiationRules.Rate(10f, 0f, 0f));
        Near(0f, RadiationRules.Rate(10f, 1f, -1f));
        Near(10f, RadiationRules.Rate(10f, 1f, 5f));
        Equal(float.MaxValue, RadiationRules.Rate(float.MaxValue, 0f, 1f));
    }

    private static void DoseAccumulation()
    {
        Near(3f, RadiationRules.Accumulate(2f, 4f, 0.25f));
        Near(2f, RadiationRules.Accumulate(2f, 4f, 0f));
        Near(2f, RadiationRules.Accumulate(2f, 0f, 20f));
        Equal(float.MaxValue, RadiationRules.Accumulate(float.MaxValue, float.MaxValue, float.MaxValue));
        float single = RadiationRules.Accumulate(0f, 2f, 10f);
        float ticks = 0f;
        for (int i = 0; i < 200; i++) ticks = RadiationRules.Accumulate(ticks, 2f, 0.05f);
        Near(single, ticks, 0.0001f);
    }

    private static void InvalidRadiation()
    {
        float[] invalid = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f };
        foreach (float value in invalid)
        {
            Throws<ArgumentOutOfRangeException>(() => RadiationRules.Stage(value, 100f));
            Throws<ArgumentOutOfRangeException>(() => RadiationRules.Stage(0f, value));
            Throws<ArgumentOutOfRangeException>(() => RadiationRules.Rate(value, 1f, 1f));
            Throws<ArgumentOutOfRangeException>(() => RadiationRules.Rate(1f, value, 1f));
            Throws<ArgumentOutOfRangeException>(() => RadiationRules.Accumulate(value, 1f, 1f));
            Throws<ArgumentOutOfRangeException>(() => RadiationRules.Accumulate(1f, value, 1f));
            Throws<ArgumentOutOfRangeException>(() => RadiationRules.Accumulate(1f, 1f, value));
        }
        Throws<ArgumentOutOfRangeException>(() => RadiationRules.Stage(0f, 0f));
        Throws<ArgumentOutOfRangeException>(() => RadiationRules.Rate(1f, 1f, float.NaN));
        Throws<ArgumentOutOfRangeException>(() => RadiationRules.Rate(1f, 1f, float.PositiveInfinity));
    }

    private static void RoleBoundaries()
    {
        Equal(0, Count(ShiftRules.AssignTraitors(4, new FixedRandom(0.0))));
        Equal(0, Count(ShiftRules.AssignTraitors(4, new FixedRandom(0.249999999999))));
        Equal(1, Count(ShiftRules.AssignTraitors(4, new FixedRandom(0.25))));
        Equal(1, Count(ShiftRules.AssignTraitors(4, new FixedRandom(0.899999999999))));
        Equal(2, Count(ShiftRules.AssignTraitors(4, new FixedRandom(0.90))));
        Equal(2, Count(ShiftRules.AssignTraitors(4, new FixedRandom(0.999999999999))));
    }

    private static void UniqueRoles()
    {
        for (int size = 4; size <= 6; size++)
        {
            bool[] roles = ShiftRules.AssignTraitors(size, new FixedRandom(0.95));
            Equal(size, roles.Length);
            Equal(2, Count(roles));
            Check(roles[size - 1] && roles[0], "Partial shuffle must select two different suits.");
        }
        Random random = new Random(1423);
        bool[] seen = new bool[6];
        for (int run = 0; run < 2000; run++)
        {
            bool[] roles = ShiftRules.AssignTraitors(6, random);
            Check(Count(roles) <= 2, "At most two traitors.");
            for (int i = 0; i < roles.Length; i++) seen[i] |= roles[i];
        }
        Equal(6, Count(seen));
    }

    private static void InvalidRoles()
    {
        Throws<ArgumentOutOfRangeException>(() => ShiftRules.AssignTraitors(3, new Random()));
        Throws<ArgumentOutOfRangeException>(() => ShiftRules.AssignTraitors(7, new Random()));
        Throws<ArgumentNullException>(() => ShiftRules.AssignTraitors(4, null));
    }

    private static void FourSuitTwoTraitors()
    {
        CrewOutcome[] suits = {
            Suit(1, true), Suit(2, true), Suit(3), Suit(4, false, 100f, false, true)
        };
        RoundOutcome outcome = ShiftRules.Resolve(true, true, false, suits, 100f);
        Check(outcome.CrewWon, "Three qualifying suits must work even with two secret traitors.");
        Equal(0, outcome.WinningTraitors.Length);
    }

    private static void CrewWinsExclusively()
    {
        RoundOutcome outcome = ShiftRules.Resolve(true, true, false, Crew(), 100f);
        Check(outcome.CrewWon, "Valid objective must win for crew.");
        Equal(0, outcome.WinningTraitors.Length);
    }

    private static void FailureTriggers()
    {
        RoundOutcome breach = ShiftRules.Resolve(false, true, false, Crew(), 100f);
        Check(!breach.CrewWon, "Delivery cannot override a breach.");
        Equal(1, breach.WinningTraitors[0]);
        RoundOutcome late = ShiftRules.Resolve(true, true, true, Crew(), 100f);
        Check(!late.CrewWon, "Delivery cannot override expiry.");
        Equal(1, late.WinningTraitors[0]);
    }

    private static void EjectedTraitor()
    {
        // Ejection is represented by successful evacuation, not incapacitation.
        CrewOutcome[] suits = { Suit(1, true, 10f, true), Suit(2), Suit(3), Suit(4) };
        RoundOutcome result = ShiftRules.Resolve(true, false, true, suits, 100f);
        Equal(1, result.WinningTraitors.Length);
        Equal(1, result.WinningTraitors[0]);
    }

    private static void IneligibleTraitors()
    {
        CrewOutcome[][] snapshots = {
            new[] { Suit(1, true, 100f), Suit(2), Suit(3), Suit(4) },
            new[] { Suit(1, true, 50f, true, true), Suit(2), Suit(3), Suit(4) },
            new[] { Suit(1, true, 10f, false), Suit(2), Suit(3), Suit(4) }
        };
        foreach (CrewOutcome[] snapshot in snapshots)
        {
            RoundOutcome result = ShiftRules.Resolve(false, false, true, snapshot, 100f);
            Check(!result.CrewWon, "Mutual failure cannot win for crew.");
            Equal(0, result.WinningTraitors.Length);
            Check(result.Summary.StartsWith("NOBODY WINS."), "Mutual failure report must be explicit.");
        }
        CrewOutcome[] two = { Suit(1, true, 100f), Suit(2, true, 99.999f), Suit(3), Suit(4) };
        RoundOutcome eligible = ShiftRules.Resolve(false, false, false, two, 100f);
        Equal(1, eligible.WinningTraitors.Length);
        Equal(2, eligible.WinningTraitors[0]);
    }

    private static void NobodyWins()
    {
        CrewOutcome[] noTraitors = { Suit(1), Suit(2), Suit(3), Suit(4) };
        RoundOutcome result = ShiftRules.Resolve(false, false, false, noTraitors, 100f);
        Check(!result.CrewWon, "Zero-traitor rounds still fail.");
        Equal(0, result.WinningTraitors.Length);
        Check(result.Summary.StartsWith("NOBODY WINS."), "Nobody-wins report is required.");
        RoundOutcome empty = ShiftRules.Resolve(false, false, true, new CrewOutcome[0], 100f);
        Equal(0, empty.WinningTraitors.Length);
    }

    private static void InsufficientSurvivors()
    {
        CrewOutcome[] suits = { Suit(1, true), Suit(2), Suit(3, false, 100f), Suit(4, false, 40f, true, true) };
        RoundOutcome result = ShiftRules.Resolve(true, true, false, suits, 100f);
        Check(!result.CrewWon, "Dose and incapacitation exclude survivors.");
        Equal(1, result.WinningTraitors[0]);
    }

    private static void UnresolvedShift()
    {
        RoundOutcome result = ShiftRules.Resolve(true, false, false, Crew(), 100f);
        Check(!result.CrewWon, "A cask in the work room is unfinished.");
        Equal(0, result.WinningTraitors.Length);
        Check(result.Summary.StartsWith("SHIFT UNRESOLVED."), "Unfinished work is not a failure trigger on its own.");
    }

    private static void InvalidOutcomes()
    {
        Throws<ArgumentNullException>(() => ShiftRules.Resolve(true, true, false, null, 100f));
        Throws<ArgumentException>(() => ShiftRules.Resolve(true, true, false, new[] { Suit(1), Suit(1) }, 100f));
        Throws<ArgumentException>(() => ShiftRules.Resolve(true, true, false, new CrewOutcome[] { null }, 100f));
        Throws<ArgumentOutOfRangeException>(() => ShiftRules.Resolve(true, true, false, Crew(), 0f));
        Throws<ArgumentOutOfRangeException>(() => new CrewOutcome(0, false, 0f, true, false));
        Throws<ArgumentOutOfRangeException>(() => new CrewOutcome(7, false, 0f, true, false));
        Throws<ArgumentOutOfRangeException>(() => new CrewOutcome(1, false, float.NaN, true, false));
        RoundOutcome result = ShiftRules.Resolve(false, false, false, Crew(), 100f);
        int[] copy = result.WinningTraitors;
        copy[0] = 6;
        Equal(1, result.WinningTraitors[0]);
    }

    private static void ValidTorque()
    {
        TorqueProcedure procedure = new TorqueProcedure();
        Check(!procedure.IsSealed, "A new cask is open.");
        for (int i = 0; i < Star.Length; i++)
        {
            procedure.Apply(Star[i], 40f);
            procedure.Apply(Star[i], 40f);
            Equal(i == Star.Length - 1, procedure.IsSealed);
        }
        procedure.Apply(7, 0f);
        Check(procedure.IsSealed, "A no-op cannot unseal.");
    }

    private static void WrongTorqueOrder()
    {
        TorqueProcedure procedure = new TorqueProcedure();
        procedure.Apply(4, 80f);
        Near(80f, procedure.TorqueAt(4));
        foreach (int bolt in Star) procedure.Apply(bolt, bolt == 4 ? 0f : 80f);
        Check(!procedure.IsSealed, "A valid final torque does not excuse a wrong sequence.");
        procedure.Reset();
        for (int i = 0; i < 8; i++) Near(0f, procedure.TorqueAt(i));
        Seal(procedure);
        Check(procedure.IsSealed, "A full reset permits another attempt.");
    }

    private static void TorqueTolerance()
    {
        TorqueProcedure procedure = new TorqueProcedure();
        procedure.Apply(0, 74f);
        Check(!procedure.IsSealed, "Below tolerance cannot seal.");
        procedure.Apply(0, 1f);
        for (int i = 1; i < Star.Length; i++) procedure.Apply(Star[i], 85f);
        Check(procedure.IsSealed, "75 and 85 are inclusive.");
        procedure.Apply(0, 11f);
        Near(86f, procedure.TorqueAt(0));
        Check(!procedure.IsSealed, "Overtorque must be possible and break containment.");
        procedure.Apply(0, -6f);
        Check(procedure.IsSealed, "Reducing overtorque without unseating preserves the sequence.");
    }

    private static void TorqueLoosening()
    {
        TorqueProcedure procedure = new TorqueProcedure();
        Seal(procedure);
        procedure.Apply(4, -6f);
        Near(74f, procedure.TorqueAt(4));
        Check(!procedure.IsSealed, "Backing off a seated bolt invalidates the seal.");
        procedure.Apply(4, 6f);
        Check(!procedure.IsSealed, "Retightening alone cannot restore sequence integrity.");
        procedure.Reset();
        Seal(procedure);
        Check(procedure.IsSealed, "Reset and full star sequence must recover.");
    }

    private static void InvalidTorque()
    {
        TorqueProcedure procedure = new TorqueProcedure();
        procedure.Apply(0, float.MaxValue);
        Near(120f, procedure.TorqueAt(0));
        procedure.Apply(0, -float.MaxValue);
        Near(0f, procedure.TorqueAt(0));
        Throws<ArgumentOutOfRangeException>(() => procedure.Apply(-1, 1f));
        Throws<ArgumentOutOfRangeException>(() => procedure.TorqueAt(8));
        Throws<ArgumentOutOfRangeException>(() => procedure.Apply(0, float.NaN));
        Throws<ArgumentOutOfRangeException>(() => procedure.Apply(0, float.PositiveInfinity));
        Near(0f, procedure.TorqueAt(0));
    }

    private static void CompleteLockout()
    {
        LockoutHold hold = new LockoutHold();
        Check(hold.Begin(1, 2), "Valid hold begins.");
        Check(!hold.Tick(1, 2, 5.999f, true), "Six seconds are required.");
        Check(hold.Begin(1, 2), "Repeated Begin for same pair preserves progress.");
        Check(hold.Tick(1, 2, 0.0011f, true), "Threshold must complete.");
        Equal(1, hold.Uses);
        Check(!hold.IsActive, "Completion closes the hold.");
        Check(!hold.Tick(1, 2, 10f, true), "Completion is a one-time event.");
        Equal(1, hold.Uses);
    }

    private static void InterruptedLockout()
    {
        LockoutHold hold = new LockoutHold();
        hold.Begin(1, 2);
        Check(!hold.Tick(1, 2, 5f, true), "Hold is incomplete.");
        Check(!hold.Tick(1, 2, 2f, false), "A disconnected target or released lever cancels.");
        Equal(0, hold.Uses);
        Check(!hold.IsActive, "Invalid hold is inactive.");
        Near(0f, hold.Elapsed);
        hold.Begin(1, 2);
        Check(!hold.Tick(1, 2, 1f, true), "Prior progress must not carry across reconnect.");
        hold.Cancel();
        Check(!hold.Tick(1, 2, 6f, true), "An explicit cancellation cannot complete.");
        Equal(0, hold.Uses);
    }

    private static void ChangedLockout()
    {
        LockoutHold hold = new LockoutHold();
        hold.Begin(1, 2);
        hold.Tick(1, 2, 5f, true);
        Check(!hold.Tick(3, 2, 1f, true), "A different actor cannot finish another actor's hold.");
        Check(!hold.IsActive, "Actor mismatch cancels.");
        hold.Begin(1, 2);
        hold.Tick(1, 2, 5f, true);
        Check(!hold.Tick(1, 3, 1f, true), "Changing the target cancels.");
        hold.Begin(1, 2);
        hold.Tick(1, 2, 5f, true);
        hold.Begin(1, 3);
        Check(!hold.Tick(1, 3, 1f, true), "Begin with a new pair starts from zero.");
        Check(hold.Tick(1, 3, 5f, true), "The replacement target needs the full hold.");
    }

    private static void ExhaustedLockout()
    {
        LockoutHold hold = new LockoutHold();
        hold.Begin(1, 2);
        Check(hold.Tick(1, 2, 6f, true), "First ejection.");
        hold.Begin(1, 3);
        Check(hold.Tick(1, 3, 6f, true), "Second ejection.");
        Equal(2, hold.Uses);
        Check(!hold.Begin(1, 4), "Third hold must not start.");
        Check(!hold.Tick(1, 4, 60f, true), "Third ejection cannot complete.");
        hold.Cancel();
        Equal(2, hold.Uses);
    }

    private static void InvalidLockout()
    {
        LockoutHold hold = new LockoutHold();
        Check(!hold.Begin(1, 1), "A suit cannot eject itself.");
        Throws<ArgumentOutOfRangeException>(() => hold.Begin(0, 2));
        Throws<ArgumentOutOfRangeException>(() => hold.Begin(1, 7));
        hold.Begin(1, 2);
        Throws<ArgumentOutOfRangeException>(() => hold.Tick(1, 2, float.NaN, true));
        Throws<ArgumentOutOfRangeException>(() => hold.Tick(1, 2, -1f, true));
        Throws<ArgumentOutOfRangeException>(() => hold.Tick(1, 2, float.PositiveInfinity, true));
        Near(0f, hold.Elapsed);
        Equal(0, hold.Uses);
    }

    private static CrewOutcome Suit(int number, bool traitor = false, float dose = 0f,
        bool evacuated = true, bool incapacitated = false)
    {
        return new CrewOutcome(number, traitor, dose, evacuated, incapacitated);
    }

    private static CrewOutcome[] Crew()
    {
        return new[] { Suit(1, true), Suit(2), Suit(3), Suit(4) };
    }

    private static void Seal(TorqueProcedure procedure)
    {
        foreach (int bolt in Star) procedure.Apply(bolt, 80f);
    }

    private static int Count(bool[] flags)
    {
        int count = 0;
        foreach (bool flag in flags) if (flag) count++;
        return count;
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception exception)
        {
            failed++;
            Console.Error.WriteLine("FAIL " + name + ": " + exception.Message);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException("Expected " + expected + "; got " + actual + ".");
    }

    private static void Near(float expected, float actual, float tolerance = 0.00001f)
    {
        if (float.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException("Expected " + expected + "; got " + actual + ".");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
    }

    private sealed class FixedRandom : Random
    {
        private readonly double roll;
        public FixedRandom(double roll) { this.roll = roll; }
        public override double NextDouble() { return roll; }
        public override int Next(int minValue, int maxValue) { return maxValue - 1; }
    }
}
