namespace TUIKit.Widgets
{
    /// <summary>
    /// A labeled bar in a <see cref="BarChart"/>: one value, or several stacked segment values.
    /// </summary>
    internal sealed class BarEntry
    {
        internal string Label { get; }

        internal double Value { get; }

        internal double[]? Segments { get; }

        internal BarEntry(string label, double value)
        {
            Label = label;
            Value = value;
        }

        internal BarEntry(string label, double[] segments)
        {
            Label = label;
            Segments = segments;
            double total = 0;
            for (int i = 0; i < segments.Length; i++)
                total += System.Math.Max(0, segments[i]);
            Value = total;
        }
    }
}
