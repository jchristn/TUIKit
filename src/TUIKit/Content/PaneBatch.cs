namespace TUIKit.Content
{
    using System;

    /// <summary>
    /// A scope that batches multiple pane writes so they are reflected together in a single rendered
    /// frame. Dispose the batch (typically with a <c>using</c> statement) to end it. While a batch is
    /// open the pane's lock is held, so the renderer will not observe a partially written block.
    /// </summary>
    public sealed class PaneBatch : IDisposable
    {
        private readonly Pane _Pane;
        private bool _Disposed;

        internal PaneBatch(Pane pane)
        {
            _Pane = pane;
        }

        /// <summary>
        /// Ends the batch, releasing the pane lock and marking the pane changed.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed)
                return;

            _Disposed = true;
            _Pane.EndBatch();
        }
    }
}
