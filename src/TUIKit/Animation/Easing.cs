namespace TUIKit.Animation
{
    using System;

    /// <summary>
    /// A library of easing functions mapping a normalized time <c>t</c> in [0, 1] to an eased
    /// progress in [0, 1]. Pass one to a <see cref="Tween"/> to shape how a value accelerates and
    /// decelerates. Inputs are clamped, so out-of-range times are safe.
    /// </summary>
    public static class Easing
    {
        /// <summary>Constant-rate progress.</summary>
        public static double Linear(double t)
        {
            return Clamp(t);
        }

        /// <summary>Accelerates from zero (quadratic).</summary>
        public static double EaseInQuad(double t)
        {
            t = Clamp(t);
            return t * t;
        }

        /// <summary>Decelerates to the target (quadratic).</summary>
        public static double EaseOutQuad(double t)
        {
            t = Clamp(t);
            return t * (2 - t);
        }

        /// <summary>Accelerates then decelerates (quadratic).</summary>
        public static double EaseInOutQuad(double t)
        {
            t = Clamp(t);
            return t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;
        }

        /// <summary>Accelerates from zero (cubic).</summary>
        public static double EaseInCubic(double t)
        {
            t = Clamp(t);
            return t * t * t;
        }

        /// <summary>Decelerates to the target (cubic).</summary>
        public static double EaseOutCubic(double t)
        {
            t = Clamp(t);
            double u = t - 1;
            return u * u * u + 1;
        }

        /// <summary>Accelerates then decelerates (cubic).</summary>
        public static double EaseInOutCubic(double t)
        {
            t = Clamp(t);
            return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        private static double Clamp(double t)
        {
            if (t < 0)
                return 0;
            if (t > 1)
                return 1;

            return t;
        }
    }
}
