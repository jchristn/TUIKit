namespace TUIKit
{
    using System;

    /// <summary>
    /// An immutable color that is either the terminal default, a 256-color palette index, or a
    /// 24-bit truecolor value. Instances are value types and are safe to share across threads.
    /// Degradation from truecolor to a palette is handled by the renderer at emit time, not here.
    /// </summary>
    public readonly struct Color : IEquatable<Color>
    {
        /// <summary>
        /// Gets the interpretation of this color.
        /// </summary>
        public ColorKind Kind { get; }

        /// <summary>
        /// Gets the red channel (0 through 255). Meaningful only when <see cref="Kind"/> is
        /// <see cref="ColorKind.Rgb"/>; otherwise zero.
        /// </summary>
        public byte R { get; }

        /// <summary>
        /// Gets the green channel (0 through 255). Meaningful only when <see cref="Kind"/> is
        /// <see cref="ColorKind.Rgb"/>; otherwise zero.
        /// </summary>
        public byte G { get; }

        /// <summary>
        /// Gets the blue channel (0 through 255). Meaningful only when <see cref="Kind"/> is
        /// <see cref="ColorKind.Rgb"/>; otherwise zero.
        /// </summary>
        public byte B { get; }

        /// <summary>
        /// Gets the palette index (0 through 255). Meaningful only when <see cref="Kind"/> is
        /// <see cref="ColorKind.Palette"/>; otherwise zero.
        /// </summary>
        public byte PaletteIndex { get; }

        /// <summary>
        /// Gets the terminal default color.
        /// </summary>
        public static Color Default
        {
            get { return new Color(ColorKind.Default, 0, 0, 0, 0); }
        }

        private Color(ColorKind kind, byte r, byte g, byte b, byte paletteIndex)
        {
            Kind = kind;
            R = r;
            G = g;
            B = b;
            PaletteIndex = paletteIndex;
        }

        /// <summary>
        /// Creates a 24-bit truecolor value.
        /// </summary>
        /// <param name="r">The red channel (0 through 255).</param>
        /// <param name="g">The green channel (0 through 255).</param>
        /// <param name="b">The blue channel (0 through 255).</param>
        /// <returns>The truecolor value.</returns>
        public static Color FromRgb(byte r, byte g, byte b)
        {
            return new Color(ColorKind.Rgb, r, g, b, 0);
        }

        /// <summary>
        /// Creates a color from a 256-color palette index.
        /// </summary>
        /// <param name="index">The palette index (0 through 255).</param>
        /// <returns>The palette color.</returns>
        public static Color FromPalette(byte index)
        {
            return new Color(ColorKind.Palette, 0, 0, 0, index);
        }

        /// <summary>
        /// Creates a truecolor value from a packed 0xRRGGBB integer.
        /// </summary>
        /// <param name="rgb">
        /// The packed color. The low 24 bits are used; higher bits are ignored. For example
        /// 0xFF8800 is red 255, green 136, blue 0.
        /// </param>
        /// <returns>The truecolor value.</returns>
        public static Color FromRgb(int rgb)
        {
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);
            return new Color(ColorKind.Rgb, r, g, b, 0);
        }

        /// <inheritdoc/>
        public bool Equals(Color other)
        {
            if (Kind != other.Kind)
                return false;

            switch (Kind)
            {
                case ColorKind.Rgb:
                    return R == other.R && G == other.G && B == other.B;
                case ColorKind.Palette:
                    return PaletteIndex == other.PaletteIndex;
                default:
                    return true;
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Color other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 31) ^ R;
                hash = (hash * 31) ^ G;
                hash = (hash * 31) ^ B;
                hash = (hash * 31) ^ PaletteIndex;
                return hash;
            }
        }

        /// <summary>
        /// Determines whether two colors are equal.
        /// </summary>
        /// <param name="left">The first color.</param>
        /// <param name="right">The second color.</param>
        /// <returns><c>true</c> if the colors are equal; otherwise <c>false</c>.</returns>
        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two colors are not equal.
        /// </summary>
        /// <param name="left">The first color.</param>
        /// <param name="right">The second color.</param>
        /// <returns><c>true</c> if the colors differ; otherwise <c>false</c>.</returns>
        public static bool operator !=(Color left, Color right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            switch (Kind)
            {
                case ColorKind.Rgb:
                    return "#" + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");
                case ColorKind.Palette:
                    return "palette:" + PaletteIndex;
                default:
                    return "default";
            }
        }
    }
}
