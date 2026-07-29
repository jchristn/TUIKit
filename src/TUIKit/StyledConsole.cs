namespace TUIKit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using TUIKit.Rendering;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    /// <summary>
    /// Writes styled, flowing output to a <see cref="TextWriter"/> at the current cursor position — the
    /// inline, one-shot counterpart to a full-screen <see cref="Hosting.TuiApplication"/>. It is the
    /// direct replacement for a styled <c>println</c>: write <see cref="StyledText"/>, parse-and-write
    /// markup, or render a whole <see cref="IWidget"/> (for example a <see cref="Widgets.Table"/>) as
    /// colored lines. Color is quantized to the resolved <see cref="ColorDepth"/>; when that is
    /// <see cref="TerminalColorDepth.None"/> (redirected output, <c>NO_COLOR</c>, or <c>TERM=dumb</c>)
    /// everything is written as plain text. This type never enters the alternate screen and never emits
    /// cursor-movement sequences — all writes go only to the supplied <see cref="TextWriter"/>.
    /// </summary>
    /// <remarks>Not thread-safe: serialize writes to a single instance externally if shared.</remarks>
    public sealed class StyledConsole
    {
        private const int MaxMeasuredHeight = 1024;
        private readonly TextWriter _Output;
        private int _DefaultWidth = 80;

        /// <summary>
        /// Initializes a new instance over an explicit writer and color depth (no auto-detection).
        /// </summary>
        /// <param name="output">The destination writer. Must not be null.</param>
        /// <param name="colorDepth">The color depth to render at; <see cref="TerminalColorDepth.None"/> forces plain text.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> is null.</exception>
        public StyledConsole(TextWriter output, TerminalColorDepth colorDepth)
        {
            _Output = output ?? throw new ArgumentNullException(nameof(output));
            ColorDepth = colorDepth;
        }

        /// <summary>
        /// Creates a writer over <see cref="Console.Out"/> with the depth resolved by
        /// <see cref="CapabilityDetector.ResolveOutputColorDepth"/> (plain when redirected, <c>NO_COLOR</c>, or <c>TERM=dumb</c>).
        /// </summary>
        /// <returns>A configured writer.</returns>
        public static StyledConsole ForStandardOutput()
        {
            return new StyledConsole(Console.Out, CapabilityDetector.ResolveOutputColorDepth(Console.Out));
        }

        /// <summary>
        /// Creates a writer over <see cref="Console.Error"/> with the depth resolved by
        /// <see cref="CapabilityDetector.ResolveOutputColorDepth"/>.
        /// </summary>
        /// <returns>A configured writer.</returns>
        public static StyledConsole ForStandardError()
        {
            return new StyledConsole(Console.Error, CapabilityDetector.ResolveOutputColorDepth(Console.Error));
        }

        /// <summary>
        /// Gets the resolved color depth. <see cref="TerminalColorDepth.None"/> means output is plain.
        /// </summary>
        public TerminalColorDepth ColorDepth { get; }

        /// <summary>
        /// Gets or sets the width used when rendering a widget and the terminal width is unknown.
        /// Defaults to 80; minimum 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to less than 1.</exception>
        public int DefaultWidth
        {
            get { return _DefaultWidth; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Default width must be at least 1.");

                _DefaultWidth = value;
            }
        }

        /// <summary>Writes styled text at the current position (no trailing newline).</summary>
        /// <param name="text">The styled text. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void Write(StyledText text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            _Output.Write(AnsiText.Render(text, ColorDepth));
        }

        /// <summary>Writes styled text followed by a newline.</summary>
        /// <param name="text">The styled text. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void WriteLine(StyledText text)
        {
            Write(text);
            _Output.Write('\n');
        }

        /// <summary>Writes a newline.</summary>
        public void WriteLine()
        {
            _Output.Write('\n');
        }

        /// <summary>Parses markup and writes it at the current position (no trailing newline).</summary>
        /// <param name="markup">The markup source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markup"/> is null.</exception>
        public void Markup(string markup)
        {
            if (markup == null)
                throw new ArgumentNullException(nameof(markup));

            Write(TUIKit.Markup.Parse(markup));
        }

        /// <summary>Parses markup and writes it followed by a newline.</summary>
        /// <param name="markup">The markup source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markup"/> is null.</exception>
        public void MarkupLine(string markup)
        {
            if (markup == null)
                throw new ArgumentNullException(nameof(markup));

            WriteLine(TUIKit.Markup.Parse(markup));
        }

        /// <summary>Writes a literal string with no markup parsing and no styling.</summary>
        /// <param name="text">The literal text. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void Write(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            _Output.Write(text);
        }

        /// <summary>Writes a literal string followed by a newline.</summary>
        /// <param name="text">The literal text. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void WriteLine(string text)
        {
            Write(text);
            _Output.Write('\n');
        }

        /// <summary>
        /// Renders a widget to colored lines and writes them (no trailing newline after the last line).
        /// </summary>
        /// <param name="widget">The widget to render. Must not be null.</param>
        /// <param name="width">The render width; defaults to the terminal width, else <see cref="DefaultWidth"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="widget"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is supplied and is less than 1.</exception>
        public void Write(IWidget widget, int? width = null)
        {
            IReadOnlyList<string> lines = RenderWidget(widget, width);
            for (int i = 0; i < lines.Count; i++)
            {
                _Output.Write(lines[i]);
                if (i < lines.Count - 1)
                    _Output.Write('\n');
            }
        }

        /// <summary>
        /// Renders a widget to colored lines and writes them, each followed by a newline.
        /// </summary>
        /// <param name="widget">The widget to render. Must not be null.</param>
        /// <param name="width">The render width; defaults to the terminal width, else <see cref="DefaultWidth"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="widget"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is supplied and is less than 1.</exception>
        public void WriteLine(IWidget widget, int? width = null)
        {
            Write(widget, width);
            _Output.Write('\n');
        }

        private IReadOnlyList<string> RenderWidget(IWidget widget, int? width)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));
            if (width.HasValue && width.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(width), width.Value, "Width must be at least 1.");

            int resolvedWidth = width ?? ResolveWidth();
            Size measured = widget.Measure(new Size(resolvedWidth, MaxMeasuredHeight));
            int height = measured.Height;
            if (height < 1)
                height = 1;
            if (height > MaxMeasuredHeight)
                height = MaxMeasuredHeight;

            CellBuffer buffer = new CellBuffer(resolvedWidth, height);
            widget.Render(new BufferSurface(buffer));
            return InlineRenderer.ToAnsiLines(buffer, ColorDepth);
        }

        private int ResolveWidth()
        {
            try
            {
                if (ReferenceEquals(_Output, Console.Out) && !Console.IsOutputRedirected)
                {
                    int width = Console.WindowWidth;
                    if (width > 0)
                        return width;
                }
            }
            catch (IOException)
            {
                // No console attached; fall back.
            }

            return _DefaultWidth;
        }
    }
}
