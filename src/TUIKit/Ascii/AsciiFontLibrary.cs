namespace TUIKit.Ascii
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    /// A thread-safe registry of ASCII-art fonts keyed by name. Look-ups are case-insensitive. The
    /// shared <see cref="Default"/> instance is pre-populated with the built-in fonts (lazily); create
    /// your own instance when you need an isolated set. Consumers register additional fonts —
    /// including <c>.flf</c> files parsed by <see cref="FigletFontLoader"/> — through
    /// <see cref="Register"/>. Reads use a reader/writer lock, so many threads may resolve fonts
    /// concurrently while registration is exclusive.
    /// </summary>
    public sealed class AsciiFontLibrary : IDisposable
    {
        private static readonly Lazy<AsciiFontLibrary> _Default =
            new Lazy<AsciiFontLibrary>(CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly ReaderWriterLockSlim _Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly Dictionary<string, IAsciiFont> _Fonts =
            new Dictionary<string, IAsciiFont>(StringComparer.OrdinalIgnoreCase);
        private bool _Disposed;

        /// <summary>
        /// Gets the shared library, pre-populated with the built-in fonts on first access. Never null.
        /// </summary>
        public static AsciiFontLibrary Default
        {
            get { return _Default.Value; }
        }

        /// <summary>
        /// Gets the number of registered fonts.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        public int Count
        {
            get
            {
                ThrowIfDisposed();
                _Lock.EnterReadLock();
                try
                {
                    return _Fonts.Count;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets a snapshot of the registered font names, in no particular order. Never null.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        public IReadOnlyList<string> Names
        {
            get
            {
                ThrowIfDisposed();
                _Lock.EnterReadLock();
                try
                {
                    return new List<string>(_Fonts.Keys);
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Registers a font, throwing if a font with the same name is already registered.
        /// </summary>
        /// <param name="font">The font to register. Must not be null and must have a non-empty name.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the font's name is null or empty.</exception>
        /// <exception cref="AsciiFontException">Thrown when a font with the same name is already registered.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        public void Register(IAsciiFont font)
        {
            IAsciiFont validated = Validate(font);
            ThrowIfDisposed();
            _Lock.EnterWriteLock();
            try
            {
                if (_Fonts.ContainsKey(validated.Name))
                    throw new AsciiFontException("A font named '" + validated.Name + "' is already registered.");

                _Fonts[validated.Name] = validated;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Attempts to register a font, returning false instead of throwing when the name is taken.
        /// </summary>
        /// <param name="font">The font to register. Must not be null and must have a non-empty name.</param>
        /// <returns><c>true</c> when registered; <c>false</c> when a font with the same name already exists.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="font"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the font's name is null or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        public bool TryRegister(IAsciiFont font)
        {
            IAsciiFont validated = Validate(font);
            ThrowIfDisposed();
            _Lock.EnterWriteLock();
            try
            {
                if (_Fonts.ContainsKey(validated.Name))
                    return false;

                _Fonts[validated.Name] = validated;
                return true;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a font by name.
        /// </summary>
        /// <param name="name">The font name. Must not be null.</param>
        /// <returns><c>true</c> when a font was removed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        public bool Unregister(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            ThrowIfDisposed();
            _Lock.EnterWriteLock();
            try
            {
                return _Fonts.Remove(name);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Determines whether a font is registered under the supplied name.
        /// </summary>
        /// <param name="name">The font name. Must not be null.</param>
        /// <returns><c>true</c> when present; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        public bool Contains(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            ThrowIfDisposed();
            _Lock.EnterReadLock();
            try
            {
                return _Fonts.ContainsKey(name);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Attempts to resolve a font by name.
        /// </summary>
        /// <param name="name">The font name. Must not be null.</param>
        /// <param name="font">When this method returns <c>true</c>, the resolved font; otherwise null.</param>
        /// <returns><c>true</c> when the font is registered; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        public bool TryGet(string name, out IAsciiFont font)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            ThrowIfDisposed();
            _Lock.EnterReadLock();
            try
            {
                return _Fonts.TryGetValue(name, out font!);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Resolves a font by name, throwing when it is not registered.
        /// </summary>
        /// <param name="name">The font name. Must not be null.</param>
        /// <returns>The resolved font.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="AsciiFontException">Thrown when no font is registered under the name.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        public IAsciiFont Get(string name)
        {
            if (!TryGet(name, out IAsciiFont font))
                throw new AsciiFontException("No font named '" + name + "' is registered.");

            return font;
        }

        /// <summary>
        /// Returns a snapshot of the registered fonts.
        /// </summary>
        /// <returns>The registered fonts. Never null.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        public IEnumerable<IAsciiFont> Enumerate()
        {
            ThrowIfDisposed();
            _Lock.EnterReadLock();
            try
            {
                return new List<IAsciiFont>(_Fonts.Values);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Asynchronously enumerates the registered fonts from a snapshot taken at call time. The async
        /// companion to <see cref="Enumerate"/>.
        /// </summary>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>An async sequence of the registered fonts.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the library has been disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
        public async IAsyncEnumerable<IAsciiFont> EnumerateAsync([EnumeratorCancellation] CancellationToken token)
        {
            List<IAsciiFont> snapshot;
            ThrowIfDisposed();
            _Lock.EnterReadLock();
            try
            {
                snapshot = new List<IAsciiFont>(_Fonts.Values);
            }
            finally
            {
                _Lock.ExitReadLock();
            }

            foreach (IAsciiFont font in snapshot)
            {
                token.ThrowIfCancellationRequested();
                yield return font;
            }

            await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Releases the reader/writer lock held by this library.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed)
                return;

            if (disposing)
                _Lock.Dispose();

            _Disposed = true;
        }

        private IAsciiFont Validate(IAsciiFont font)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));
            if (string.IsNullOrEmpty(font.Name))
                throw new ArgumentException("The font must have a non-empty name.", nameof(font));

            return font;
        }

        private void ThrowIfDisposed()
        {
            if (_Disposed)
                throw new ObjectDisposedException(nameof(AsciiFontLibrary));
        }

        private static AsciiFontLibrary CreateDefault()
        {
            AsciiFontLibrary library = new AsciiFontLibrary();
            foreach (IAsciiFont font in BuiltInFonts.All())
                library.TryRegister(font);

            return library;
        }
    }
}
