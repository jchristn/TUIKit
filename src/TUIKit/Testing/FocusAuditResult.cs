namespace TUIKit.Testing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The outcome of a <see cref="FocusAudit"/> run. Immutable.
    /// </summary>
    public sealed class FocusAuditResult
    {
        private readonly List<FocusAuditProblem> _Problems;

        /// <summary>
        /// Gets the number of distinct focus stops visited by Tab in one full cycle.
        /// </summary>
        public int Stops { get; }

        /// <summary>
        /// Gets the defects found, in the order seen. Never null; empty when the audit is clean.
        /// </summary>
        public IReadOnlyList<FocusAuditProblem> Problems
        {
            get { return _Problems; }
        }

        /// <summary>
        /// Gets a value indicating whether no defects were found.
        /// </summary>
        public bool IsClean
        {
            get { return _Problems.Count == 0; }
        }

        internal FocusAuditResult(int stops, List<FocusAuditProblem> problems)
        {
            Stops = stops;
            _Problems = problems;
        }

        /// <summary>
        /// Throws when the audit found defects, listing every one, so a test can assert with one call.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="IsClean"/> is false.</exception>
        public void ThrowIfProblems()
        {
            if (IsClean)
                return;

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append("Focus audit found ").Append(_Problems.Count).Append(" problem(s) over ").Append(Stops).Append(" stop(s):");
            for (int i = 0; i < _Problems.Count; i++)
                builder.Append('\n').Append(_Problems[i]);

            throw new InvalidOperationException(builder.ToString());
        }
    }
}
