namespace TUIKit.Input
{
    /// <summary>
    /// Synthesizes single, double, and triple click counts from button-press events, since terminals
    /// report only presses and releases. The caller supplies the event and a millisecond timestamp;
    /// consecutive presses on the same cell within <see cref="ThresholdMilliseconds"/> increment the
    /// count. Keeping timing external makes the behavior deterministic to test.
    /// </summary>
    /// <remarks>Not thread-safe; drive from the single input thread.</remarks>
    public sealed class ClickSynthesizer
    {
        private int _ThresholdMilliseconds = 400;
        private long _LastTimestamp = long.MinValue;
        private int _LastX = int.MinValue;
        private int _LastY = int.MinValue;
        private MouseButton _LastButton = MouseButton.None;
        private int _Count;

        /// <summary>
        /// Gets or sets the maximum gap in milliseconds between presses that still counts as a
        /// multi-click. Defaults to 400. Must be greater than zero.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when set to a non-positive value.</exception>
        public int ThresholdMilliseconds
        {
            get { return _ThresholdMilliseconds; }
            set
            {
                if (value <= 0)
                    throw new System.ArgumentOutOfRangeException(nameof(value), value, "Threshold must be greater than zero.");
                _ThresholdMilliseconds = value;
            }
        }

        /// <summary>
        /// Registers a press and returns the resulting click count (1 for single, 2 for double, 3 for
        /// triple, then cycling back to 1).
        /// </summary>
        /// <param name="button">The button pressed.</param>
        /// <param name="x">The zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="timestampMilliseconds">A monotonically increasing millisecond timestamp.</param>
        /// <returns>The click count.</returns>
        public int RegisterPress(MouseButton button, int x, int y, long timestampMilliseconds)
        {
            bool sameSpot = x == _LastX && y == _LastY && button == _LastButton;
            bool inTime = timestampMilliseconds - _LastTimestamp <= _ThresholdMilliseconds;

            if (sameSpot && inTime && _Count >= 1 && _Count < 3)
                _Count++;
            else
                _Count = 1;

            _LastTimestamp = timestampMilliseconds;
            _LastX = x;
            _LastY = y;
            _LastButton = button;
            return _Count;
        }

        /// <summary>
        /// Resets the click state so the next press starts a new sequence.
        /// </summary>
        public void Reset()
        {
            _Count = 0;
            _LastTimestamp = long.MinValue;
            _LastX = int.MinValue;
            _LastY = int.MinValue;
            _LastButton = MouseButton.None;
        }
    }
}
