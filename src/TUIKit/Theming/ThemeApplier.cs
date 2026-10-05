namespace TUIKit.Theming
{
    using System;

    /// <summary>
    /// Applies a <see cref="Theme"/> to any object that implements <see cref="IThemeable"/>. Objects that
    /// do not implement it are left unchanged, so it is safe to call on every widget in a tree.
    /// </summary>
    public static class ThemeApplier
    {
        /// <summary>
        /// Applies the theme when <paramref name="target"/> implements <see cref="IThemeable"/>.
        /// </summary>
        /// <param name="target">The object to theme, or null (ignored).</param>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <returns><c>true</c> when the target was themed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public static bool Apply(object? target, Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            if (target is IThemeable themeable)
            {
                themeable.ApplyTheme(theme);
                return true;
            }

            return false;
        }
    }
}
