namespace TUIKit.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Terminal;

    /// <summary>
    /// A recorded sequence of raw input chunks with inter-chunk delays, used to reproduce a session
    /// deterministically for bug reports or an unattended demo. Feed a recording back through a
    /// backend with <see cref="Replay"/>.
    /// </summary>
    public sealed class InputRecording
    {
        private readonly List<int> _Delays = new List<int>();
        private readonly List<byte[]> _Chunks = new List<byte[]>();

        /// <summary>
        /// Gets the number of recorded chunks.
        /// </summary>
        public int Count
        {
            get { return _Chunks.Count; }
        }

        /// <summary>
        /// Appends a chunk of raw input.
        /// </summary>
        /// <param name="data">The raw bytes. Must not be null.</param>
        /// <param name="delayMillisecondsBefore">The delay before this chunk. Must be zero or greater.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the delay is negative.</exception>
        public void Add(byte[] data, int delayMillisecondsBefore)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (delayMillisecondsBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(delayMillisecondsBefore), delayMillisecondsBefore, "Delay must be zero or greater.");

            byte[] copy = new byte[data.Length];
            Array.Copy(data, copy, data.Length);
            _Chunks.Add(copy);
            _Delays.Add(delayMillisecondsBefore);
        }

        /// <summary>
        /// Feeds every recorded chunk into the supplied headless backend in order. Delays are ignored
        /// so replay is instantaneous and deterministic; use the delays for a paced live demo.
        /// </summary>
        /// <param name="backend">The headless backend to feed. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="backend"/> is null.</exception>
        public void Replay(HeadlessBackend backend)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));

            for (int i = 0; i < _Chunks.Count; i++)
                backend.FeedInput(_Chunks[i]);
        }

        /// <summary>
        /// Gets the delay recorded before the chunk at the supplied index.
        /// </summary>
        /// <param name="index">The chunk index.</param>
        /// <returns>The delay in milliseconds.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public int DelayAt(int index)
        {
            if (index < 0 || index >= _Delays.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _Delays[index];
        }
    }
}
