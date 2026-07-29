namespace TUIKit.Widgets
{
    /// <summary>
    /// Selects which glyph family <see cref="Icons"/> returns, trading fidelity for portability.
    /// </summary>
    public enum IconMode
    {
        /// <summary>Nerd Font private-use glyphs. Requires a patched Nerd Font in the terminal.</summary>
        Nerd = 0,

        /// <summary>Widely supported Unicode symbols (BMP). The safe default for SSH and tmux.</summary>
        Unicode = 1,

        /// <summary>Plain ASCII stand-ins. Works on any terminal, including legacy code pages.</summary>
        Ascii = 2
    }
}
