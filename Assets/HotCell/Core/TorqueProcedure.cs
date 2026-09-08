using System;

namespace HotCell.Core
{
    /// <summary>
    /// An intentionally fallible physical procedure. All torque changes are accepted;
    /// the procedure gives no role-dependent permissions or sequence correction.
    /// Wrong order, or loosening a seated bolt below 75, invalidates the attempt until Reset.
    /// Reset represents backing off all eight bolts; it clears every torque and the sequence.
    /// </summary>
    public sealed class TorqueProcedure
    {
        public const int BoltCount = 8;
        public const float TargetTorque = 80f;
        public const float Tolerance = 5f;
        public const float MaximumTorque = 120f;
        private static readonly int[] StarOrder = { 0, 4, 2, 6, 1, 5, 3, 7 };
        private readonly float[] torques = new float[BoltCount];
        private readonly bool[] seated = new bool[BoltCount];
        private int nextBolt;
        private bool invalidated;

        public bool IsSealed
        {
            get
            {
                if (invalidated || nextBolt != BoltCount) return false;
                for (int i = 0; i < BoltCount; i++)
                    if (torques[i] < TargetTorque - Tolerance || torques[i] > TargetTorque + Tolerance)
                        return false;
                return true;
            }
        }

        public void Apply(int bolt, float amount)
        {
            ValidateBolt(bolt);
            Numeric.RequireFinite(amount, "amount");
            float previous = torques[bolt];
            float current = (float)Math.Max(0.0, Math.Min(MaximumTorque, previous + (double)amount));
            torques[bolt] = current;
            float threshold = TargetTorque - Tolerance;

            if (seated[bolt] && current < threshold) invalidated = true;
            if (previous < threshold && current >= threshold && !seated[bolt])
            {
                seated[bolt] = true;
                if (nextBolt < BoltCount && bolt == StarOrder[nextBolt]) nextBolt++;
                else invalidated = true;
            }
        }

        public float TorqueAt(int bolt)
        {
            ValidateBolt(bolt);
            return torques[bolt];
        }

        public void Reset()
        {
            Array.Clear(torques, 0, torques.Length);
            Array.Clear(seated, 0, seated.Length);
            nextBolt = 0;
            invalidated = false;
        }

        private static void ValidateBolt(int bolt)
        {
            if (bolt < 0 || bolt >= BoltCount) throw new ArgumentOutOfRangeException("bolt");
        }
    }
}
