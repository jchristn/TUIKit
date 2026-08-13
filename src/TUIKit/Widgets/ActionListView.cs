namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A selectable list whose rows expose keyboard actions in addition to navigation — for example
    /// Enter to open, "e" to edit, Delete to remove. Register each action with a key chord, an
    /// identifier, and an optional predicate that decides whether it applies to a given row; when a
    /// registered chord fires on an eligible row the <see cref="Activated"/> event is raised with a
    /// typed <see cref="ListAction{T}"/> saying which row and which action. Enter is registered as the
    /// built-in "activate" action.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public sealed class ActionListView<T> : IWidget, IFocusable, IFocusAware
    {
        /// <summary>
        /// The identifier of the built-in activate action bound to Enter.
        /// </summary>
        public const string ActivateActionId = "activate";

        private readonly ListView<T> _List;
        private readonly List<KeyChord> _Chords = new List<KeyChord>();
        private readonly List<string> _ActionIds = new List<string>();
        private readonly List<Func<T, bool>?> _Predicates = new List<Func<T, bool>?>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionListView{T}"/> class.
        /// </summary>
        /// <param name="display">
        /// A selector mapping an item to its label, or null when <typeparamref name="T"/> is <see cref="string"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="display"/> is null and <typeparamref name="T"/> is not <see cref="string"/>.
        /// </exception>
        public ActionListView(Func<T, string>? display = null)
        {
            _List = new ListView<T>(display);
            RegisterAction(KeyChord.FromKeyEvent(KeyEvent.Special(KeyCode.Enter)), ActivateActionId);
        }

        /// <summary>
        /// Raised when a registered action fires on an eligible row.
        /// </summary>
        public event Action<ListAction<T>>? Activated;

        /// <summary>
        /// Gets or sets a value indicating whether the list is focused. Forwarded to the inner list.
        /// </summary>
        public bool IsFocused
        {
            get { return _List.IsFocused; }
            set { _List.IsFocused = value; }
        }

        /// <summary>
        /// Gets the inner list, exposing item binding, selection, and styling.
        /// </summary>
        public ListView<T> List
        {
            get { return _List; }
        }

        /// <summary>
        /// Gets the items. Never null.
        /// </summary>
        public IReadOnlyList<T> Items
        {
            get { return _List.Items; }
        }

        /// <summary>
        /// Gets the zero-based selected index, or -1 when empty.
        /// </summary>
        public int SelectedIndex
        {
            get { return _List.SelectedIndex; }
        }

        /// <summary>
        /// Replaces the items and resets selection.
        /// </summary>
        /// <param name="items">The items. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public void SetItems(IEnumerable<T> items)
        {
            _List.SetItems(items);
        }

        /// <summary>
        /// Registers a row action. The same chord registered twice keeps the later binding (last wins).
        /// </summary>
        /// <param name="chord">The key chord that fires the action.</param>
        /// <param name="actionId">The action identifier. Must not be null or empty.</param>
        /// <param name="isEnabled">
        /// An optional predicate deciding whether the action applies to a given item; null means always
        /// enabled.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="actionId"/> is null or empty.</exception>
        public void RegisterAction(KeyChord chord, string actionId, Func<T, bool>? isEnabled = null)
        {
            if (string.IsNullOrEmpty(actionId))
                throw new ArgumentException("Action id must not be null or empty.", nameof(actionId));

            for (int i = 0; i < _Chords.Count; i++)
            {
                if (_Chords[i].Equals(chord))
                {
                    _ActionIds[i] = actionId;
                    _Predicates[i] = isEnabled;
                    return;
                }
            }

            _Chords.Add(chord);
            _ActionIds.Add(actionId);
            _Predicates.Add(isEnabled);
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            _List.OnFocusChanged(focused);
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            KeyChord chord = KeyChord.FromKeyEvent(key);
            for (int i = 0; i < _Chords.Count; i++)
            {
                if (!_Chords[i].Equals(chord))
                    continue;

                int index = _List.SelectedIndex;
                if (index < 0)
                    return true;

                T item = _List.Items[index];
                Func<T, bool>? predicate = _Predicates[i];
                if (predicate != null && !predicate(item))
                    return true;

                Activated?.Invoke(new ListAction<T>(index, item, _ActionIds[i]));
                return true;
            }

            return _List.HandleKey(key);
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return _List.Measure(available);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            _List.Render(surface);
        }
    }
}
