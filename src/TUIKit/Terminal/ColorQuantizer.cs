namespace TUIKit.Terminal
{
    /// <summary>
    /// Converts 24-bit truecolor values to lower-fidelity palettes for terminals that cannot render
    /// truecolor. All members are thread-safe.
    /// </summary>
    public static class ColorQuantizer
    {
        private static readonly int[] _CubeSteps = { 0x00, 0x5F, 0x87, 0xAF, 0xD7, 0xFF };

        /// <summary>
        /// Maps an RGB color to the nearest index in the xterm 256-color palette.
        /// </summary>
        /// <param name="r">The red channel (0 through 255).</param>
        /// <param name="g">The green channel (0 through 255).</param>
        /// <param name="b">The blue channel (0 through 255).</param>
        /// <returns>The palette index (16 through 255).</returns>
        public static byte ToPalette256(byte r, byte g, byte b)
        {
            int cubeIndex = (36 * NearestCubeStep(r)) + (6 * NearestCubeStep(g)) + NearestCubeStep(b) + 16;

            int gray = (int)((0.299 * r) + (0.587 * g) + (0.114 * b));
            int grayIndex;
            if (gray < 8)
                grayIndex = 232;
            else if (gray > 238)
                grayIndex = 255;
            else
                grayIndex = 232 + ((gray - 8) / 10);

            int cubeR = _CubeSteps[NearestCubeStep(r)];
            int cubeG = _CubeSteps[NearestCubeStep(g)];
            int cubeB = _CubeSteps[NearestCubeStep(b)];
            int grayLevel = 8 + (10 * (grayIndex - 232));

            int cubeDistance = Distance(r, g, b, cubeR, cubeG, cubeB);
            int grayDistance = Distance(r, g, b, grayLevel, grayLevel, grayLevel);

            return (byte)(grayDistance < cubeDistance ? grayIndex : cubeIndex);
        }

        /// <summary>
        /// Maps an RGB color to the nearest index in the standard 16-color ANSI palette.
        /// </summary>
        /// <param name="r">The red channel (0 through 255).</param>
        /// <param name="g">The green channel (0 through 255).</param>
        /// <param name="b">The blue channel (0 through 255).</param>
        /// <returns>The palette index (0 through 15).</returns>
        public static byte ToAnsi16(byte r, byte g, byte b)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                int pr = _Ansi16[i * 3];
                int pg = _Ansi16[(i * 3) + 1];
                int pb = _Ansi16[(i * 3) + 2];
                int distance = Distance(r, g, b, pr, pg, pb);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return (byte)best;
        }

        private static readonly int[] _Ansi16 =
        {
            0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0x00, 0x80, 0x80, 0x00,
            0x00, 0x00, 0x80, 0x80, 0x00, 0x80, 0x00, 0x80, 0x80, 0xC0, 0xC0, 0xC0,
            0x80, 0x80, 0x80, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0x00,
            0x00, 0x00, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
        };

        private static int NearestCubeStep(int value)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < _CubeSteps.Length; i++)
            {
                int distance = System.Math.Abs(value - _CubeSteps[i]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private static int Distance(int r1, int g1, int b1, int r2, int g2, int b2)
        {
            int dr = r1 - r2;
            int dg = g1 - g2;
            int db = b1 - b2;
            return (dr * dr) + (dg * dg) + (db * db);
        }
    }
}
