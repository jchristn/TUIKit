namespace TUIKit.Widgets
{
    /// <summary>
    /// Which weekday rows of a <see cref="CalendarHeatMap"/> carry a label. See <see cref="CalendarHeatMap.WeekdayLabels"/>.
    /// </summary>
    public enum WeekdayLabelMode
    {
        /// <summary>No weekday labels.</summary>
        None = 0,

        /// <summary>Every other row (the second, fourth, and sixth), as contribution calendars do. The default.</summary>
        Alternate = 1,

        /// <summary>Every row.</summary>
        All = 2
    }
}
