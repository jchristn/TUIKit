namespace TUIKit.Content
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using TUIKit.Diagnostics;

    /// <summary>
    /// Reads and writes the host clipboard. Writing is terminal-native: <see cref="BuildWriteSequence"/>
    /// produces an OSC 52 escape that works over SSH and tmux (the same mechanism as
    /// <see cref="ClipboardWriter"/>). Reading OSC 52 is not universally supported, so
    /// <see cref="TryReadText"/> shells out to the platform clipboard tool (pbpaste on macOS,
    /// xclip/xsel on Linux, Get-Clipboard on Windows) and quietly returns <c>false</c> when none is
    /// available. Reads never throw and never block indefinitely.
    /// </summary>
    public static class SystemClipboard
    {
        /// <summary>
        /// Builds the OSC 52 escape sequence that sets the terminal clipboard to the given text.
        /// </summary>
        /// <param name="text">The text to copy. Must not be null.</param>
        /// <returns>The escape sequence to write to the terminal.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static string BuildWriteSequence(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return ClipboardWriter.BuildSequence(text);
        }

        /// <summary>
        /// Attempts to read the host clipboard by invoking the platform clipboard tool.
        /// </summary>
        /// <param name="text">When this method returns, the clipboard text, or empty on failure.</param>
        /// <returns><c>true</c> when the clipboard was read; otherwise <c>false</c>.</returns>
        public static bool TryReadText(out string text)
        {
            text = string.Empty;

            string fileName;
            string arguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fileName = "powershell";
                arguments = "-NoProfile -Command Get-Clipboard";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                fileName = "pbpaste";
                arguments = string.Empty;
            }
            else
            {
                fileName = "xclip";
                arguments = "-selection clipboard -o";
            }

            if (TryRun(fileName, arguments, out text))
                return true;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return TryRun("xsel", "--clipboard --output", out text);

            return false;
        }

        private static bool TryRun(string fileName, string arguments, out string output)
        {
            long start = TuiKitInstruments.Timestamp();
            Activity? span = TuiKitInstruments.StartActivity(TuiKitTelemetryNames.ServiceClipboard + " " + TuiKitTelemetryNames.OperationRead, ActivityKind.Client);
            TuiKitInstruments.SetTag(span, TuiKitTelemetryNames.AttrIntegrationService, TuiKitTelemetryNames.ServiceClipboard);
            TuiKitInstruments.SetTag(span, TuiKitTelemetryNames.AttrIntegrationOperation, TuiKitTelemetryNames.OperationRead);
            TuiKitInstruments.SetTag(span, TuiKitTelemetryNames.AttrProcessExecutable, fileName);

            string outcome = TuiKitTelemetryNames.OutcomeError;
            string? errorType = null;
            try
            {
                bool succeeded = TryRunCore(fileName, arguments, out output, out outcome, out errorType);
                if (succeeded)
                    TuiKitInstruments.MarkOk(span);
                else
                    TuiKitInstruments.MarkError(span, errorType ?? outcome, "Clipboard tool " + fileName + " did not return text (" + outcome + ").");
                return succeeded;
            }
            finally
            {
                TuiKitInstruments.RecordIntegration(TuiKitTelemetryNames.ServiceClipboard, TuiKitTelemetryNames.OperationRead, outcome, errorType, TuiKitInstruments.SecondsSince(start));
                TuiKitInstruments.Stop(span);
            }
        }

        private static bool TryRunCore(string fileName, string arguments, out string output, out string outcome, out string? errorType)
        {
            output = string.Empty;
            outcome = TuiKitTelemetryNames.OutcomeError;
            errorType = null;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process? process = Process.Start(info))
                {
                    if (process == null)
                    {
                        errorType = "process_not_started";
                        return false;
                    }

                    string result = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(1000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException ex)
                        {
                            TuiKitInstruments.RecordError(TuiKitTelemetryNames.ComponentIntegration, ex);
                        }

                        outcome = TuiKitTelemetryNames.OutcomeTimeout;
                        errorType = "timeout";
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        errorType = "process_exit_nonzero";
                        return false;
                    }

                    output = result.TrimEnd('\r', '\n');
                    outcome = TuiKitTelemetryNames.OutcomeOk;
                    return true;
                }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException || ex is System.IO.IOException)
            {
                errorType = TuiKitInstruments.ErrorType(ex);
                TuiKitInstruments.RecordError(TuiKitTelemetryNames.ComponentIntegration, ex);
                return false;
            }
        }
    }
}
