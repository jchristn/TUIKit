namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using TUIKit.Diagnostics;

    /// <summary>
    /// The default <see cref="IFileSystemProvider"/> backed by the real disk. Roots are the machine's
    /// ready drives on Windows (with volume labels when available) or the single root <c>/</c> elsewhere.
    /// Children are the sorted directories then files; an unreadable directory yields an empty child set
    /// rather than throwing. The path comparer is <see cref="StringComparer.OrdinalIgnoreCase"/> on
    /// Windows and <see cref="StringComparer.Ordinal"/> elsewhere. Not thread-safe.
    /// </summary>
    public sealed class FileSystemProvider : IFileSystemProvider
    {
        private static readonly bool _Windows = Path.DirectorySeparatorChar == '\\';

        /// <inheritdoc/>
        public IEqualityComparer<string> PathComparer
        {
            get { return _Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal; }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetRoots()
        {
            long start = TuiKitInstruments.Timestamp();
            Activity? span = TuiKitInstruments.StartActivity(TuiKitTelemetryNames.ServiceFileSystem + " " + TuiKitTelemetryNames.OperationGetRoots, ActivityKind.Client);
            string? errorType = null;
            List<string> roots = new List<string>();
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                for (int i = 0; i < drives.Length; i++)
                {
                    DriveInfo drive = drives[i];
                    if (drive.IsReady)
                        roots.Add(drive.RootDirectory.FullName);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                errorType = TuiKitInstruments.ErrorType(ex);
                TuiKitInstruments.RecordError(TuiKitTelemetryNames.ComponentIntegration, ex);
                TuiKitInstruments.MarkError(span, ex);
            }

            if (roots.Count == 0)
                roots.Add(_Windows ? "C:\\" : "/");

            EndCall(TuiKitTelemetryNames.OperationGetRoots, start, span, errorType, roots.Count);
            return roots;
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetChildren(string path, bool includeFiles, bool includeHidden)
        {
            if (string.IsNullOrEmpty(path))
                return Array.Empty<string>();

            long start = TuiKitInstruments.Timestamp();
            Activity? span = TuiKitInstruments.StartActivity(TuiKitTelemetryNames.ServiceFileSystem + " " + TuiKitTelemetryNames.OperationGetChildren, ActivityKind.Client);
            List<string> directories = new List<string>();
            List<string> files = new List<string>();
            try
            {
                directories.AddRange(Directory.GetDirectories(path));
                if (includeFiles)
                    files.AddRange(Directory.GetFiles(path));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is ArgumentException)
            {
                TuiKitInstruments.RecordError(TuiKitTelemetryNames.ComponentIntegration, ex);
                TuiKitInstruments.MarkError(span, ex);
                EndCall(TuiKitTelemetryNames.OperationGetChildren, start, span, TuiKitInstruments.ErrorType(ex), 0);
                return Array.Empty<string>();
            }

            directories.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> result = new List<string>(directories.Count + files.Count);
            for (int i = 0; i < directories.Count; i++)
            {
                if (includeHidden || !IsHidden(directories[i]))
                    result.Add(directories[i]);
            }

            for (int i = 0; i < files.Count; i++)
            {
                if (includeHidden || !IsHidden(files[i]))
                    result.Add(files[i]);
            }

            EndCall(TuiKitTelemetryNames.OperationGetChildren, start, span, null, result.Count);
            return result;
        }

        private static void EndCall(string operation, long start, Activity? span, string? errorType, int resultCount)
        {
            string outcome = errorType == null ? TuiKitTelemetryNames.OutcomeOk : TuiKitTelemetryNames.OutcomeError;
            TuiKitInstruments.SetTag(span, TuiKitTelemetryNames.AttrIntegrationService, TuiKitTelemetryNames.ServiceFileSystem);
            TuiKitInstruments.SetTag(span, TuiKitTelemetryNames.AttrIntegrationOperation, operation);
            TuiKitInstruments.SetTag(span, TuiKitTelemetryNames.AttrResultCount, resultCount);
            if (errorType == null)
                TuiKitInstruments.MarkOk(span);
            TuiKitInstruments.RecordIntegration(TuiKitTelemetryNames.ServiceFileSystem, operation, outcome, errorType, TuiKitInstruments.SecondsSince(start));
            TuiKitInstruments.Stop(span);
        }

        /// <inheritdoc/>
        public bool IsDirectory(string path)
        {
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }

        /// <inheritdoc/>
        public string DisplayName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string trimmed = TrimTrailingSeparator(path);
            string name = Path.GetFileName(trimmed);
            if (!string.IsNullOrEmpty(name))
                return name;

            string label = TryVolumeLabel(path);
            return label.Length > 0 ? path + " (" + label + ")" : path;
        }

        /// <inheritdoc/>
        public string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (ArgumentException)
            {
                return path;
            }
            catch (NotSupportedException)
            {
                return path;
            }
            catch (PathTooLongException)
            {
                return path;
            }

            return TrimTrailingSeparator(full);
        }

        private static string TrimTrailingSeparator(string path)
        {
            if (path.Length <= 1)
                return path;

            int end = path.Length;
            while (end > 1 && (path[end - 1] == Path.DirectorySeparatorChar || path[end - 1] == Path.AltDirectorySeparatorChar))
                end--;

            // Keep the separator for a drive root such as "C:\".
            if (_Windows && end == 2 && path[1] == ':')
                return path.Substring(0, 3 <= path.Length ? 3 : path.Length);

            return path.Substring(0, end);
        }

        private static bool IsHidden(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string TryVolumeLabel(string path)
        {
            try
            {
                DriveInfo drive = new DriveInfo(path);
                if (drive.IsReady)
                    return drive.VolumeLabel ?? string.Empty;
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return string.Empty;
        }
    }
}
