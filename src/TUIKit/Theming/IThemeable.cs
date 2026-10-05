namespace TUIKit.Theming
{
    /// <summary>
    /// Implemented by widgets (and other visual components) whose colors can follow a <see cref="Theme"/>.
    /// <see cref="ApplyTheme"/> copies the theme's role styles into the component's style properties, so a
    /// later theme switch restyles it without the application touching each property. Containers forward
    /// the call to their children. A component that is never handed a theme keeps its built-in defaults,
    /// so theming is opt-in: call <see cref="ThemeApplier.Apply"/> yourself, or set
    /// <see cref="Hosting.TuiApplication.ApplyThemeToWidgets"/> to let the host do it on bind and on every
    /// theme change. Styles set after <see cref="ApplyTheme"/> stay until the next call.
    /// </summary>
    public interface IThemeable
    {
        /// <summary>
        /// Copies the theme's role styles into this component's style properties.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        void ApplyTheme(Theme theme);
    }
}
