using System;

namespace HotCell.Core
{
    public enum DoseStage
    {
        Clear,
        Noise,
        Tremor,
        Failing,
        Collapsed
    }

    /// <summary>Game units only; these values do not model real radiation safety.</summary>
    public static class RadiationRules
    {
        // A half-metre source radius prevents a singularity at its centre.
        public const float MinimumDistanceSquared = 0.25f;

        public static DoseStage Stage(float dose, float ceiling)
        {
            Numeric.RequireNonNegative(dose, "dose");
            Numeric.RequirePositive(ceiling, "ceiling");
            if (dose == 0f) return DoseStage.Clear;
            // Compare in the caller's float units, including the non-exact 0.95f boundary.
            if (dose < ceiling * 0.25f) return DoseStage.Clear;
            if (dose < ceiling * 0.50f) return DoseStage.Noise;
            if (dose < ceiling * 0.75f) return DoseStage.Tremor;
            if (dose < ceiling * 0.95f) return DoseStage.Failing;
            return DoseStage.Collapsed;
        }

        public static float Rate(float strength, float distanceSquared, float transmission)
        {
            Numeric.RequireNonNegative(strength, "strength");
            Numeric.RequireNonNegative(distanceSquared, "distanceSquared");
            Numeric.RequireFinite(transmission, "transmission");
            double factor = Math.Max(0.0, Math.Min(1.0, transmission));
            double denominator = Math.Max(MinimumDistanceSquared, distanceSquared);
            return Numeric.Saturate(strength * factor / denominator);
        }

        public static float Accumulate(float dose, float rate, float dt)
        {
            Numeric.RequireNonNegative(dose, "dose");
            Numeric.RequireNonNegative(rate, "rate");
            Numeric.RequireNonNegative(dt, "dt");
            return Numeric.Saturate(dose + (double)rate * dt);
        }
    }

    internal static class Numeric
    {
        internal static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "Must be finite.");
        }

        internal static void RequireNonNegative(float value, string name)
        {
            RequireFinite(value, name);
            if (value < 0f)
                throw new ArgumentOutOfRangeException(name, "Must be nonnegative.");
        }

        internal static void RequirePositive(float value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0f)
                throw new ArgumentOutOfRangeException(name, "Must be positive.");
        }

        internal static float Saturate(double value)
        {
            return (float)Math.Min(float.MaxValue, value);
        }
    }
}
