using System;

namespace HotCell.Core
{
    /// <summary>Host-ticked uninterrupted hold. Only a completed ejection consumes a use.</summary>
    public sealed class LockoutHold
    {
        public const int MaximumUses = 2;
        public const float RequiredSeconds = 6f;
        private double elapsed;

        public int Uses { get; private set; }
        public bool IsActive { get; private set; }
        public int Actor { get; private set; }
        public int Target { get; private set; }
        public float Elapsed { get { return (float)elapsed; } }

        public LockoutHold()
        {
            Cancel();
        }

        public bool Begin(int actor, int target)
        {
            ValidateSuit(actor, "actor");
            ValidateSuit(target, "target");
            if (actor == target || Uses >= MaximumUses) return false;
            if (IsActive && Actor == actor && Target == target) return true;
            Cancel();
            IsActive = true;
            Actor = actor;
            Target = target;
            return true;
        }

        /// <summary>
        /// valid must include host checks for input still held, actor reach, and both suits
        /// connected and eligible. Any interruption cancels; completion returns true once.
        /// </summary>
        public bool Tick(int actor, int target, float dt, bool valid)
        {
            Numeric.RequireNonNegative(dt, "dt");
            if (!IsActive) return false;
            if (!valid || actor != Actor || target != Target)
            {
                Cancel();
                return false;
            }
            elapsed += dt;
            if (elapsed < RequiredSeconds) return false;
            Uses++;
            Cancel();
            return true;
        }

        public void Cancel()
        {
            IsActive = false;
            Actor = 0;
            Target = 0;
            elapsed = 0.0;
        }

        private static void ValidateSuit(int number, string name)
        {
            if (number < 1 || number > 6)
                throw new ArgumentOutOfRangeException(name, "Suit numbers must be 1 through 6.");
        }
    }
}
