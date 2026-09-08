using System;
using System.Collections.Generic;

namespace HotCell.Core
{
    public sealed class CrewOutcome
    {
        public int Number { get; private set; }
        public bool Traitor { get; private set; }
        public float Dose { get; private set; }
        public bool Evacuated { get; private set; }
        public bool Incapacitated { get; private set; }

        public CrewOutcome(int number, bool traitor, float dose, bool evacuated, bool incapacitated)
        {
            if (number < 1 || number > 6)
                throw new ArgumentOutOfRangeException("number", "Suit numbers must be 1 through 6.");
            Numeric.RequireNonNegative(dose, "dose");
            Number = number;
            Traitor = traitor;
            Dose = dose;
            Evacuated = evacuated;
            Incapacitated = incapacitated;
        }
    }

    public sealed class RoundOutcome
    {
        private readonly int[] winningTraitors;

        public bool CrewWon { get; private set; }
        // A caller cannot mutate an already resolved outcome through the returned array.
        public int[] WinningTraitors { get { return (int[])winningTraitors.Clone(); } }
        public string Summary { get; private set; }

        internal RoundOutcome(bool crewWon, int[] winners, string summary)
        {
            CrewWon = crewWon;
            winningTraitors = (int[])winners.Clone();
            Summary = summary;
        }
    }

    public static class ShiftRules
    {
        public const int RequiredSurvivors = 3;

        /// <summary>Host-only assignment; send each suit only its own result until the report.</summary>
        public static bool[] AssignTraitors(int players, Random random)
        {
            if (players < 4 || players > 6)
                throw new ArgumentOutOfRangeException("players", "A shift supports 4 through 6 suits.");
            if (random == null) throw new ArgumentNullException("random");

            double roll = random.NextDouble();
            int count = roll < 0.25 ? 0 : roll < 0.90 ? 1 : 2;
            bool[] result = new bool[players];
            int[] candidates = new int[players];
            for (int i = 0; i < players; i++) candidates[i] = i;
            // A partial shuffle cannot select the same suit twice.
            for (int i = 0; i < count; i++)
            {
                int selected = random.Next(i, players);
                int suit = candidates[selected];
                candidates[selected] = candidates[i];
                candidates[i] = suit;
                result[suit] = true;
            }
            return result;
        }

        /// <summary>
        /// Evaluate a final shift snapshot. Every living evacuee below the ceiling counts
        /// toward the three-suit requirement, irrespective of secret role or ejection.
        /// A traitor must also have evacuated alive below the ceiling to win a failed shift.
        /// </summary>
        public static RoundOutcome Resolve(bool contained, bool delivered, bool expired,
            IReadOnlyList<CrewOutcome> crew, float ceiling)
        {
            if (crew == null) throw new ArgumentNullException("crew");
            Numeric.RequirePositive(ceiling, "ceiling");
            if (crew.Count > 6)
                throw new ArgumentOutOfRangeException("crew", "At most six suits may participate.");

            int survivors = 0;
            HashSet<int> numbers = new HashSet<int>();
            List<int> traitors = new List<int>();
            for (int i = 0; i < crew.Count; i++)
            {
                CrewOutcome suit = crew[i];
                if (suit == null) throw new ArgumentException("A suit outcome cannot be null.", "crew");
                if (!numbers.Add(suit.Number))
                    throw new ArgumentException("Each suit number must be unique.", "crew");
                bool eligible = suit.Evacuated && !suit.Incapacitated && suit.Dose < ceiling;
                if (!eligible) continue;
                survivors++;
                if (suit.Traitor) traitors.Add(suit.Number);
            }

            if (contained && delivered && !expired && survivors >= RequiredSurvivors)
                return new RoundOutcome(true, new int[0], "CREW WINS. Cask contained and delivered; at least three suits evacuated under the dose ceiling.");

            string failure;
            if (!contained) failure = "Containment breached.";
            else if (expired) failure = "Shift timer expired.";
            else if (survivors < RequiredSurvivors) failure = "Fewer than three suits evacuated alive under the dose ceiling.";
            else
                return new RoundOutcome(false, new int[0], "SHIFT UNRESOLVED. Cask has not reached transport.");

            traitors.Sort();
            if (traitors.Count == 0)
                return new RoundOutcome(false, new int[0], "NOBODY WINS. " + failure + " No eligible traitor survived extraction.");
            return new RoundOutcome(false, traitors.ToArray(), "TRAITOR WINS. " + failure);
        }
    }
}
