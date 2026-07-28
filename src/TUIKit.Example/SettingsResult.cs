namespace TUIKit.Example
{
    /// <summary>
    /// The values chosen in the settings modal.
    /// </summary>
    internal sealed class SettingsResult
    {
        internal string Theme { get; }

        internal bool AsciiBorders { get; }

        internal string Label { get; }

        internal SettingsResult(string theme, bool asciiBorders, string label)
        {
            Theme = theme;
            AsciiBorders = asciiBorders;
            Label = label;
        }
    }
}
