namespace TUIKit.Input
{
    using System;

    /// <summary>
    /// A clickable link in pane content. A link carries display text and any combination of a target
    /// URI (opened in the browser), a command identifier (dispatched through the routing table), and
    /// an opaque payload the application can switch on. The live click path is virtual hit-testing;
    /// OSC 8 output is a degradation path emitted by the renderer.
    /// </summary>
    public sealed class Link
    {
        /// <summary>
        /// Gets the link identifier, unique within a frame's registry.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the display text of the link. Never null.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the target URI, or null when the link has none.
        /// </summary>
        public string? Uri { get; }

        /// <summary>
        /// Gets the command identifier dispatched when the link is activated, or null.
        /// </summary>
        public string? CommandId { get; }

        /// <summary>
        /// Gets an opaque application payload, or null.
        /// </summary>
        public object? Payload { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Link"/> class.
        /// </summary>
        /// <param name="id">The link identifier.</param>
        /// <param name="text">The display text. Must not be null.</param>
        /// <param name="uri">The target URI, or null.</param>
        /// <param name="commandId">The command identifier, or null.</param>
        /// <param name="payload">An opaque payload, or null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public Link(int id, string text, string? uri = null, string? commandId = null, object? payload = null)
        {
            Id = id;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Uri = uri;
            CommandId = commandId;
            Payload = payload;
        }
    }
}
