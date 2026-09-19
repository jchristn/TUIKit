namespace TUIKit.Rendering
{
    using System;
    using System.Text;
    using TUIKit.Terminal;

    /// <summary>
    /// Double-buffered renderer that composes a frame into a back buffer, diffs it against what is
    /// currently on screen, and emits the minimal set of escape sequences to reconcile the two. Only
    /// changed rows are repainted, and SGR style changes are coalesced so unchanged styling is not
    /// re-emitted.
    /// </summary>
    /// <remarks>
    /// Not thread-safe. All rendering occurs on a single render thread; panes marshal cross-thread
    /// writes into their own state, which the compositor reads while building each frame.
    /// </remarks>
    public sealed class TerminalRenderer
    {
        private readonly TerminalColorDepth _ColorDepth;
        private CellBuffer _Front;
        private CellBuffer _Back;
        private Size _Size;
        private bool _RepaintPending;

        /// <summary>
        /// Gets the size of the surface the renderer is currently composing.
        /// </summary>
        public Size Size
        {
            get { return _Size; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether each emitted frame is wrapped in a synchronized
        /// update (DEC private mode 2026), so the terminal presents the frame atomically without
        /// tearing. Defaults to false. Only set this when the backend reports
        /// <see cref="TerminalCapabilities.SynchronizedOutput"/>; an unchanged (empty) frame is never
        /// wrapped, so no begin/end pair is emitted when there is nothing to present.
        /// </summary>
        public bool SynchronizedOutput { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether every frame repaints all rows regardless of whether
        /// their content changed. Defaults to false, so only changed rows are emitted. Turn it on for
        /// backends that drop or corrupt incremental updates (for example some ConPTY/Windows Terminal
        /// configurations that leave stale cells behind), trading extra output for correctness. This is
        /// a persistent setting; the one-shot <see cref="Invalidate"/> forces only the next frame.
        /// </summary>
        public bool ForceFullRepaint { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TerminalRenderer"/> class.
        /// </summary>
        /// <param name="width">The initial width in cells. Must be greater than zero.</param>
        /// <param name="height">The initial height in cells. Must be greater than zero.</param>
        /// <param name="colorDepth">The color depth to target when emitting SGR sequences.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
        public TerminalRenderer(int width, int height, TerminalColorDepth colorDepth)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");

            _ColorDepth = colorDepth;
            _Size = new Size(width, height);
            _Front = new CellBuffer(width, height);
            _Back = new CellBuffer(width, height);
            _RepaintPending = true;
        }

        /// <summary>
        /// Composes and emits one frame. The draw callback receives a surface over the back buffer,
        /// which is cleared to blanks before the callback runs. The resulting diff is written to the
        /// backend and flushed.
        /// </summary>
        /// <param name="backend">The backend to write to. Must not be null.</param>
        /// <param name="draw">The composition callback. Must not be null.</param>
        /// <returns><c>true</c> when any output was emitted; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="backend"/> or <paramref name="draw"/> is null.
        /// </exception>
        public bool Render(ITerminalBackend backend, Action<ISurface> draw)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));
            if (draw == null)
                throw new ArgumentNullException(nameof(draw));

            SyncSize(backend.Size);

            _Back.Clear(CellStyle.Default);
            BufferSurface surface = new BufferSurface(_Back);
            draw(surface);

            string output = BuildDiff();
            _Front.CopyFrom(_Back);
            _RepaintPending = false;

            if (output.Length == 0)
                return false;

            // A begin/end synchronized-update pair wraps the whole frame in a single buffered write so
            // the terminal presents it atomically. Emitting the pair only around a non-empty diff means
            // an unchanged frame stays a true no-op. The pair is always balanced within one write, so a
            // frame can never leave the terminal in a held state between frames.
            if (SynchronizedOutput)
                backend.Write(Ansi.BeginSynchronizedUpdate + output + Ansi.EndSynchronizedUpdate);
            else
                backend.Write(output);

            backend.Flush();
            return true;
        }

        /// <summary>
        /// Forces the next frame to repaint every row, regardless of whether content changed. Used
        /// after a screen clear or a return from a suspended state. For an always-on full repaint see
        /// <see cref="ForceFullRepaint"/>.
        /// </summary>
        public void Invalidate()
        {
            _RepaintPending = true;
        }

        private void SyncSize(Size size)
        {
            if (size.Width <= 0 || size.Height <= 0)
                return;

            if (size == _Size)
                return;

            _Size = size;
            _Front.Resize(size.Width, size.Height);
            _Back.Resize(size.Width, size.Height);
            _RepaintPending = true;
        }

        private string BuildDiff()
        {
            StringBuilder builder = new StringBuilder();
            bool full = _RepaintPending || ForceFullRepaint;

            for (int row = 0; row < _Size.Height; row++)
            {
                if (!full && RowsEqual(row))
                    continue;

                EmitRow(builder, row);
            }

            if (builder.Length > 0)
                builder.Append(Ansi.ResetAttributes);

            return builder.ToString();
        }

        private bool RowsEqual(int row)
        {
            for (int col = 0; col < _Size.Width; col++)
            {
                if (!_Front.Get(col, row).Equals(_Back.Get(col, row)))
                    return false;
            }

            return true;
        }

        private void EmitRow(StringBuilder builder, int row)
        {
            builder.Append(Ansi.MoveTo(0, row));

            bool penInitialized = false;
            CellStyle pen = CellStyle.Default;
            int col = 0;

            while (col < _Size.Width)
            {
                Cell cell = _Back.Get(col, row);
                if (cell.IsContinuation)
                {
                    col++;
                    continue;
                }

                if (!penInitialized || cell.Style != pen)
                {
                    builder.Append(Ansi.Sgr(cell.Style, _ColorDepth));
                    pen = cell.Style;
                    penInitialized = true;
                }

                builder.Append(string.IsNullOrEmpty(cell.Grapheme) ? " " : cell.Grapheme);
                col += cell.Width > 0 ? cell.Width : 1;
            }
        }
    }
}
