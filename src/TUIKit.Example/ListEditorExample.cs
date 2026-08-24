namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Modals;

    /// <summary>
    /// Builds an example <see cref="ListEditorModal{T}"/>: a string tag editor with a legend and a
    /// validating parser. It shows the single-screen add/remove flow with a live preview, and is launched
    /// from the harness with <c>ShowAsync&lt;IReadOnlyList&lt;string&gt;&gt;</c> so the returned list (or
    /// null on cancel) can be echoed into the log.
    /// </summary>
    internal static class ListEditorExample
    {
        /// <summary>
        /// Creates the configured tag editor, seeded with a couple of example tags.
        /// </summary>
        /// <returns>The configured modal.</returns>
        internal static ListEditorModal<string> Create()
        {
            ListEditorOptions<string> options = new ListEditorOptions<string>();
            options.Help = new List<string>
            {
                "Tags label a backup set. Examples:",
                "  photos   nightly   offsite-2024"
            };
            options.Describe = value => "tag \"" + value + "\"";
            options.Parse = text =>
            {
                string trimmed = text.Trim();
                if (trimmed.Length == 0)
                    return ParseResult<string>.Failure("A tag cannot be empty.");
                if (trimmed.Contains(" "))
                    return ParseResult<string>.Failure("A tag cannot contain spaces.");

                return ParseResult<string>.Success(trimmed);
            };
            options.AddPrompt = "New tag";
            options.EmptyText = "No tags yet — press 'a' to add one.";

            ListEditorModal<string> modal = new ListEditorModal<string>(
                new[] { "photos", "nightly" },
                value => value,
                options);
            modal.Title = "Edit tags";
            return modal;
        }
    }
}
