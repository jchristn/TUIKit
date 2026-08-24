namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Input;

    /// <summary>
    /// Behavior and key bindings for <see cref="ListEditorModal{T}"/>. Every knob is a public property
    /// backed by a private field with a sensible default, so a consumer overrides only what it needs.
    /// The most important switch is <see cref="Parse"/>: when it is null the editor is remove/reorder
    /// only; when it is supplied the inline Add affordance is enabled. This type is not thread-safe;
    /// configure it before handing it to a modal and do not mutate it while the modal is open.
    /// </summary>
    /// <typeparam name="T">The list item type.</typeparam>
    public sealed class ListEditorOptions<T>
    {
        private string _AddPrompt = "New item";
        private string _EmptyText = "No items yet.";
        private KeyChord _AddKey = KeyChord.Parse("a");
        private KeyChord _AddKeyAlt = KeyChord.Parse("insert");
        private KeyChord _RemoveKey = KeyChord.Parse("d");
        private KeyChord _RemoveKeyAlt = KeyChord.Parse("delete");

        /// <summary>
        /// Gets or sets the parser that turns typed text into an item. Returning
        /// <see cref="ParseResult{T}.Failure"/> keeps the buffer and shows the message inline; returning
        /// <see cref="ParseResult{T}.Success"/> appends the value (subject to the dedupe policy). When
        /// null (the default) the Add affordance is disabled and hidden, leaving a remove/reorder-only
        /// editor. Values may contain spaces; the buffer is passed verbatim with no tokenizing.
        /// </summary>
        public Func<string, ParseResult<T>>? Parse { get; set; }

        /// <summary>
        /// Gets or sets a selector for an item's secondary, plain-English text. It is shown after each
        /// item in the list and as the live "→" preview while typing. Defaults to null (no secondary text).
        /// </summary>
        public Func<T, string>? Describe { get; set; }

        /// <summary>
        /// Gets or sets the legend lines rendered above the list, for example usage examples. Defaults to
        /// null (no legend).
        /// </summary>
        public IReadOnlyList<string>? Help { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether items may be reordered with Alt+Up / Alt+Down. Defaults
        /// to <c>false</c>.
        /// </summary>
        public bool AllowReorder { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether duplicate items are accepted. Duplicates are detected
        /// with <see cref="DedupeComparer"/>. Defaults to <c>false</c>.
        /// </summary>
        public bool AllowDuplicates { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether finishing with zero items is permitted. When
        /// <c>false</c>, Enter does not close the editor while the list is empty. Defaults to <c>true</c>.
        /// </summary>
        public bool AllowEmpty { get; set; } = true;

        /// <summary>
        /// Gets or sets the comparer used to detect duplicates. Defaults to
        /// <see cref="EqualityComparer{T}.Default"/> when null.
        /// </summary>
        public IEqualityComparer<T>? DedupeComparer { get; set; }

        /// <summary>
        /// Gets or sets the prompt label shown next to the inline add field. Defaults to "New item". Must
        /// not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string AddPrompt
        {
            get { return _AddPrompt; }
            set { _AddPrompt = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the placeholder shown when the list is empty. Defaults to "No items yet.". Must
        /// not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string EmptyText
        {
            get { return _EmptyText; }
            set { _EmptyText = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the primary chord that opens the add field from Browse mode. Defaults to "a".
        /// </summary>
        public KeyChord AddKey
        {
            get { return _AddKey; }
            set { _AddKey = value; }
        }

        /// <summary>
        /// Gets or sets the alternate chord that opens the add field. Defaults to "Insert".
        /// </summary>
        public KeyChord AddKeyAlt
        {
            get { return _AddKeyAlt; }
            set { _AddKeyAlt = value; }
        }

        /// <summary>
        /// Gets or sets the primary chord that removes the selected item. Defaults to "d".
        /// </summary>
        public KeyChord RemoveKey
        {
            get { return _RemoveKey; }
            set { _RemoveKey = value; }
        }

        /// <summary>
        /// Gets or sets the alternate chord that removes the selected item. Defaults to "Delete".
        /// </summary>
        public KeyChord RemoveKeyAlt
        {
            get { return _RemoveKeyAlt; }
            set { _RemoveKeyAlt = value; }
        }
    }
}
