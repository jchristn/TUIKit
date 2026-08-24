namespace TUIKit.Example
{
    using TUIKit.Modals;

    /// <summary>
    /// Builds an example <see cref="FileSelectModal"/> over the real file system: a "choose folders and
    /// files to back up" selector rooted at the machine's ready drives. It is launched from the harness
    /// with <c>ShowAsync&lt;FileSelection&gt;</c> so the returned includes and excluded holes (or null on
    /// cancel) can be echoed into the log.
    /// </summary>
    internal static class FileSelectExample
    {
        /// <summary>
        /// Creates the configured file selector.
        /// </summary>
        /// <returns>The configured modal.</returns>
        internal static FileSelectModal Create()
        {
            FileSelectOptions options = new FileSelectOptions();
            options.Title = "Select folders and files";
            options.ShowFiles = true;
            options.ShowHidden = false;
            return new FileSelectModal(options);
        }
    }
}
