namespace TUIKit.Testing
{
    using System;
    using TUIKit.Widgets;

    /// <summary>
    /// One defect found by <see cref="FocusAudit"/>. Immutable.
    /// </summary>
    public sealed class FocusAuditProblem
    {
        /// <summary>
        /// Gets the zero-based stop at which the problem was seen (its position in the Tab order).
        /// </summary>
        public int Stop { get; }

        /// <summary>
        /// Gets the kind of defect.
        /// </summary>
        public FocusAuditProblemKind Kind { get; }

        /// <summary>
        /// Gets the focus path at that stop. Never null.
        /// </summary>
        public FocusPath Path { get; }

        /// <summary>
        /// Gets a readable explanation. Never null.
        /// </summary>
        public string Detail { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FocusAuditProblem"/> class.
        /// </summary>
        /// <param name="stop">The zero-based stop.</param>
        /// <param name="kind">The kind of defect.</param>
        /// <param name="path">The focus path at that stop. Must not be null.</param>
        /// <param name="detail">A readable explanation. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> or <paramref name="detail"/> is null.</exception>
        public FocusAuditProblem(int stop, FocusAuditProblemKind kind, FocusPath path, string detail)
        {
            Stop = stop;
            Kind = kind;
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
        }

        /// <summary>
        /// Returns <c>stop N: Kind (path): detail</c>.
        /// </summary>
        /// <returns>The problem as text. Never null.</returns>
        public override string ToString()
        {
            return "stop " + Stop + ": " + Kind + " (" + Path + "): " + Detail;
        }
    }
}
