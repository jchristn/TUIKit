namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Browses the file system: it lists the current directory (parent link first, then folders, then
    /// files, each alphabetical), moves the selection with Up/Down, descends into a folder or the
    /// parent with Enter, and goes up with Backspace or Left. Selecting a file raises
    /// <see cref="FileActivated"/> and records <see cref="SelectedPath"/>. Directory reads that fail
    /// (for example, permission denied) leave the listing empty rather than throwing. Paths use the
    /// platform separators, so it works on Windows, Linux, and macOS.
    /// </summary>
    public sealed class FileBrowser : IWidget, IFocusable
    {
        private readonly List<FileEntry> _Entries = new List<FileEntry>();
        private string _CurrentDirectory = string.Empty;
        private int _Selected;
        private int _Top;

        /// <summary>
        /// Raised when a file (not a directory) is activated with Enter. The argument is its full path.
        /// </summary>
        public event Action<string>? FileActivated;

        /// <summary>Gets the current directory being listed.</summary>
        public string CurrentDirectory
        {
            get { return _CurrentDirectory; }
        }

        /// <summary>Gets the number of listed entries (including the parent link).</summary>
        public int Count
        {
            get { return _Entries.Count; }
        }

        /// <summary>
        /// Gets the full path of the most recently activated file, or null if none has been activated.
        /// </summary>
        public string? SelectedPath { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileBrowser"/> class.
        /// </summary>
        /// <param name="startDirectory">The directory to open. Must exist.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="startDirectory"/> is null.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist.</exception>
        public FileBrowser(string startDirectory)
        {
            if (startDirectory == null)
                throw new ArgumentNullException(nameof(startDirectory));
            if (!Directory.Exists(startDirectory))
                throw new DirectoryNotFoundException("Directory not found: " + startDirectory);

            Navigate(startDirectory);
        }

        /// <summary>
        /// Gets the name of the selected entry, or null when the listing is empty.
        /// </summary>
        public string? SelectedName
        {
            get { return _Entries.Count == 0 ? null : _Entries[_Selected].Name; }
        }

        /// <summary>
        /// Lists the given directory, resetting the selection to the top.
        /// </summary>
        /// <param name="directory">The directory to list. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="directory"/> is null.</exception>
        public void Navigate(string directory)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));

            _CurrentDirectory = Path.GetFullPath(directory);
            _Entries.Clear();
            _Selected = 0;
            _Top = 0;

            DirectoryInfo? parent = Directory.GetParent(_CurrentDirectory);
            if (parent != null)
                _Entries.Add(new FileEntry("..", parent.FullName, true, true));

            List<string> directories = new List<string>();
            List<string> files = new List<string>();
            try
            {
                directories.AddRange(Directory.GetDirectories(_CurrentDirectory));
                files.AddRange(Directory.GetFiles(_CurrentDirectory));
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }

            directories.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < directories.Count; i++)
                _Entries.Add(new FileEntry(Path.GetFileName(directories[i]), directories[i], true, false));
            for (int i = 0; i < files.Count; i++)
                _Entries.Add(new FileEntry(Path.GetFileName(files[i]), files[i], false, false));
        }

        /// <summary>
        /// Activates the current selection: descends into a directory, ascends via the parent link, or
        /// raises <see cref="FileActivated"/> for a file.
        /// </summary>
        public void ActivateSelected()
        {
            if (_Entries.Count == 0)
                return;

            FileEntry entry = _Entries[_Selected];
            if (entry.IsDirectory)
            {
                Navigate(entry.FullPath);
            }
            else
            {
                SelectedPath = entry.FullPath;
                FileActivated?.Invoke(entry.FullPath);
            }
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up:
                    _Selected = Math.Max(0, _Selected - 1);
                    return true;
                case KeyCode.Down:
                    _Selected = Math.Min(Math.Max(0, _Entries.Count - 1), _Selected + 1);
                    return true;
                case KeyCode.Enter:
                    ActivateSelected();
                    return true;
                case KeyCode.Left:
                case KeyCode.Backspace:
                    DirectoryInfo? parent = Directory.GetParent(_CurrentDirectory);
                    if (parent != null)
                        Navigate(parent.FullName);
                    return true;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            surface.DrawText(0, 0, Fit(_CurrentDirectory, width), CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true));

            int listHeight = height - 1;
            if (listHeight <= 0)
                return;

            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + listHeight)
                _Top = _Selected - listHeight + 1;

            for (int row = 0; row < listHeight && _Top + row < _Entries.Count; row++)
            {
                int index = _Top + row;
                FileEntry entry = _Entries[index];
                bool selected = index == _Selected;
                int y = row + 1;

                CellStyle style = selected
                    ? CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6))
                    : entry.IsDirectory ? CellStyle.Default.WithForeground(Color.FromPalette(4)) : CellStyle.Default;

                if (selected)
                    surface.Fill(new Rect(0, y, width, 1), Cell.Blank(style));

                string icon = entry.IsDirectory ? Icons.Folder.ToString() : Icons.File.ToString();
                surface.DrawText(0, y, Fit(icon + " " + entry.Name, width), style);
            }
        }

        private static string Fit(string text, int width)
        {
            if (text.Length <= width)
                return text;
            if (width <= 1)
                return text.Substring(0, Math.Max(0, width));

            return "…" + text.Substring(text.Length - (width - 1));
        }
    }
}
