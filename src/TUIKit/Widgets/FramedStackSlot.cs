namespace TUIKit.Widgets
{
    using System;
    using TUIKit.Input;
    using TUIKit.Layout;

    // One child of a FramedStack. The stack's FocusScope holds slots, not children, so a child the layout
    // dropped for lack of room reports itself hidden and focus skips (and repair leaves) it. The slot also
    // remembers where the child was last laid out, for mouse routing.
    internal sealed class FramedStackSlot : IFocusContainer, IFocusAware, IFocusPathNode, IHideable, IEnableable, IFocusStop, IFocusChildren
    {
        internal FramedStackSlot(IWidget widget, StackSize size, string? title)
        {
            Widget = widget;
            Size = size;
            Title = title;
        }

        internal IWidget Widget { get; }

        internal StackSize Size { get; }

        internal string? Title { get; }

        internal bool LaidOut { get; set; }

        internal Rect Outer { get; set; }

        internal Rect Content { get; set; }

        public bool IsVisible
        {
            get { return LaidOut && !(Widget is IHideable hideable && !hideable.IsVisible); }
        }

        public bool IsEnabled
        {
            get { return !(Widget is IEnableable enableable) || enableable.IsEnabled; }
            set
            {
                if (Widget is IEnableable enableable)
                    enableable.IsEnabled = value;
            }
        }

        public bool IsFocusStop
        {
            get { return !(Widget is IFocusStop stop) || stop.IsFocusStop; }
        }

        public bool HasFocusableChild
        {
            get { return !(Widget is IFocusChildren children) || children.HasFocusableChild; }
        }

        public bool HasTabStopChild
        {
            get { return !(Widget is IFocusChildren children) || children.HasTabStopChild; }
        }

        public IFocusable? FocusedChild
        {
            get { return Widget as IFocusable; }
        }

        public IFocusable? FocusedLeaf
        {
            get
            {
                if (Widget is IFocusContainer container)
                    return container.FocusedLeaf ?? container;

                return Widget as IFocusable;
            }
        }

        public bool HandleKey(KeyEvent key)
        {
            return Widget is IFocusable focusable && focusable.HandleKey(key);
        }

        public bool MoveFocus(bool forward)
        {
            return Widget is IFocusContainer container && container.MoveFocus(forward);
        }

        public void FocusEdge(bool first)
        {
            if (Widget is IFocusContainer container)
                container.FocusEdge(first);
        }

        public void OnFocusChanged(bool focused)
        {
            if (Widget is IFocusAware aware)
                aware.OnFocusChanged(focused);
        }
    }
}
