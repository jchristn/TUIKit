namespace TUIKit.Terminal
{
    using System;
    using System.IO;

    /// <summary>
    /// Infers <see cref="TerminalCapabilities"/> from environment variables. Detection is heuristic
    /// because terminals do not advertise their features uniformly; the detector errs toward the
    /// capabilities of modern tier-1 terminals when signals are ambiguous. All members are thread-safe.
    /// </summary>
    public static class CapabilityDetector
    {
        /// <summary>
        /// Detects capabilities using the process environment.
        /// </summary>
        /// <param name="interactive">Whether the terminal is interactive. When false, a minimal set is returned.</param>
        /// <returns>The detected capabilities.</returns>
        public static TerminalCapabilities Detect(bool interactive)
        {
            return Detect(Environment.GetEnvironmentVariable, interactive);
        }

        /// <summary>
        /// Detects capabilities using a supplied environment accessor, enabling deterministic tests.
        /// </summary>
        /// <param name="getEnvironmentVariable">
        /// A function returning the value of an environment variable, or null when unset. Must not be null.
        /// </param>
        /// <param name="interactive">Whether the terminal is interactive. When false, a minimal set is returned.</param>
        /// <returns>The detected capabilities.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="getEnvironmentVariable"/> is null.</exception>
        public static TerminalCapabilities Detect(Func<string, string?> getEnvironmentVariable, bool interactive)
        {
            if (getEnvironmentVariable == null)
                throw new ArgumentNullException(nameof(getEnvironmentVariable));

            if (!interactive)
                return new TerminalCapabilities(TerminalColorDepth.None, false, false, false, false, false);

            string? term = getEnvironmentVariable("TERM");
            if (string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase))
                return new TerminalCapabilities(TerminalColorDepth.None, false, false, false, false, false);

            string? colorTerm = getEnvironmentVariable("COLORTERM");
            string? termProgram = getEnvironmentVariable("TERM_PROGRAM");
            bool isWindowsTerminal = !string.IsNullOrEmpty(getEnvironmentVariable("WT_SESSION"));
            bool isKitty = !string.IsNullOrEmpty(getEnvironmentVariable("KITTY_WINDOW_ID"));

            TerminalColorDepth depth = DetectColorDepth(term, colorTerm, isWindowsTerminal);

            // Honor the NO_COLOR convention (https://no-color.org): when NO_COLOR is present and
            // non-empty, disable color regardless of other signals such as COLORTERM=truecolor.
            if (!string.IsNullOrEmpty(getEnvironmentVariable("NO_COLOR")))
                depth = TerminalColorDepth.None;

            bool enhancedKeyboard = isWindowsTerminal || isKitty || IsModernProgram(termProgram);

            bool modern = depth == TerminalColorDepth.TrueColor || enhancedKeyboard;
            bool sgrMouse = true;
            bool hyperlinks = modern;
            bool clipboard = true;
            bool bracketedPaste = true;

            return new TerminalCapabilities(depth, enhancedKeyboard, sgrMouse, hyperlinks, clipboard, bracketedPaste);
        }

        /// <summary>
        /// Resolves the color depth appropriate for one-shot styled output to the supplied writer.
        /// Returns <see cref="TerminalColorDepth.None"/> when the writer is redirected or is not the
        /// console output/error stream, when <c>NO_COLOR</c> is set, or when <c>TERM=dumb</c>;
        /// otherwise the environment-detected depth.
        /// </summary>
        /// <param name="output">The target writer. Must not be null.</param>
        /// <returns>The resolved color depth.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> is null.</exception>
        public static TerminalColorDepth ResolveOutputColorDepth(TextWriter output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            bool interactive;
            if (ReferenceEquals(output, Console.Out))
                interactive = !Console.IsOutputRedirected;
            else if (ReferenceEquals(output, Console.Error))
                interactive = !Console.IsErrorRedirected;
            else
                interactive = false;

            return Detect(interactive).ColorDepth;
        }

        private static TerminalColorDepth DetectColorDepth(string? term, string? colorTerm, bool isWindowsTerminal)
        {
            if (isWindowsTerminal)
                return TerminalColorDepth.TrueColor;

            if (!string.IsNullOrEmpty(colorTerm)
                && (colorTerm!.IndexOf("truecolor", StringComparison.OrdinalIgnoreCase) >= 0
                    || colorTerm.IndexOf("24bit", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return TerminalColorDepth.TrueColor;
            }

            if (!string.IsNullOrEmpty(term))
            {
                if (term!.IndexOf("256", StringComparison.OrdinalIgnoreCase) >= 0)
                    return TerminalColorDepth.Palette256;

                return TerminalColorDepth.Ansi16;
            }

            return TerminalColorDepth.Ansi16;
        }

        private static bool IsModernProgram(string? termProgram)
        {
            if (string.IsNullOrEmpty(termProgram))
                return false;

            return termProgram!.IndexOf("WezTerm", StringComparison.OrdinalIgnoreCase) >= 0
                || termProgram.IndexOf("ghostty", StringComparison.OrdinalIgnoreCase) >= 0
                || termProgram.IndexOf("kitty", StringComparison.OrdinalIgnoreCase) >= 0
                || termProgram.IndexOf("iTerm", StringComparison.OrdinalIgnoreCase) >= 0
                || termProgram.IndexOf("Alacritty", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
