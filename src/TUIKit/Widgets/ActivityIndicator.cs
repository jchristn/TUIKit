namespace TUIKit.Widgets
{
    using System;
    using TUIKit;

    /// <summary>
    /// An animated activity line: a spinner glyph followed by an optional rotating status phrase, for
    /// "working…" states. Advance it once per frame with <see cref="Tick"/>; the spinner steps every
    /// tick and the phrase rotates every <see cref="PhraseIntervalTicks"/> ticks. Animation is driven by
    /// explicit ticks rather than wall-clock time, so rendering is deterministic and testable. Read
    /// <see cref="CurrentLine"/> to push the same text into a scrollback pane through a line handle.
    /// </summary>
    public sealed class ActivityIndicator : IWidget
    {
        private static readonly string[] DefaultFrames =
        {
            "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"
        };

        private string[] _Frames;
        private string[] _Phrases = Array.Empty<string>();
        private int _PhraseIntervalTicks = 30;
        private int _Tick;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityIndicator"/> class with a braille
        /// spinner and no phrase.
        /// </summary>
        public ActivityIndicator()
        {
            _Frames = DefaultFrames;
        }

        /// <summary>
        /// Gets or sets the spinner frames. Must not be null or empty. Defaults to a braille spinner.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty array.</exception>
        public string[] Frames
        {
            get { return _Frames; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (value.Length == 0)
                    throw new ArgumentException("At least one frame is required.", nameof(value));
                _Frames = value;
            }
        }

        /// <summary>
        /// Gets or sets the rotating status phrases, or an empty array for none. Must not be null.
        /// Defaults to empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string[] Phrases
        {
            get { return _Phrases; }
            set { _Phrases = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the number of ticks between phrase changes. Must be greater than zero. Defaults to 30.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to zero or less.</exception>
        public int PhraseIntervalTicks
        {
            get { return _PhraseIntervalTicks; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Phrase interval must be greater than zero.");
                _PhraseIntervalTicks = value;
            }
        }

        /// <summary>
        /// Gets or sets the style for the rendered line. Defaults to cyan.
        /// </summary>
        public CellStyle Style { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>
        /// Gets the current spinner glyph for this tick.
        /// </summary>
        public string CurrentFrame
        {
            get { return _Frames[_Tick % _Frames.Length]; }
        }

        /// <summary>
        /// Gets the current phrase for this tick, or an empty string when no phrases are set.
        /// </summary>
        public string CurrentPhrase
        {
            get
            {
                if (_Phrases.Length == 0)
                    return string.Empty;

                int index = (_Tick / _PhraseIntervalTicks) % _Phrases.Length;
                return _Phrases[index];
            }
        }

        /// <summary>
        /// Gets the current rendered line: the spinner glyph, and the phrase when one is present.
        /// </summary>
        public string CurrentLine
        {
            get
            {
                string phrase = CurrentPhrase;
                return phrase.Length == 0 ? CurrentFrame : CurrentFrame + " " + phrase;
            }
        }

        /// <summary>
        /// Advances the animation by one tick.
        /// </summary>
        public void Tick()
        {
            _Tick++;
            if (_Tick < 0)
                _Tick = 0;
        }

        /// <summary>
        /// Resets the animation to its first frame and phrase.
        /// </summary>
        public void Reset()
        {
            _Tick = 0;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int width = TUIKit.Unicode.Graphemes.MeasureWidth(CurrentLine);
            return new Size(Math.Min(width, available.Width), 1);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (surface.Size.Width <= 0 || surface.Size.Height <= 0)
                return;

            surface.DrawText(0, 0, CurrentLine, Style);
        }
    }
}
