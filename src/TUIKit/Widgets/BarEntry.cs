namespace TUIKit.Widgets
{
    /// <summary>
    /// A single labeled value in a <see cref="BarChart"/>.
    /// </summary>
    internal sealed class BarEntry
    {
        internal string Label { get; }

        internal double Value { get; }

        internal BarEntry(string label, double value)
        {
            Label = label;
            Value = value;
        }
    }
}
