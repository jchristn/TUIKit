namespace TUIKit.Widgets
{
    // One frame to draw in a FramedStack render pass. Frames from nested stacks are collected into the
    // root's list so every unfocused frame is drawn before the one focused frame, which then stays whole.
    internal sealed class FramedStackFrame
    {
        internal FramedStackFrame(FramedStack owner, Rect outer, string? title, bool focused)
        {
            Owner = owner;
            Outer = outer;
            Title = title;
            Focused = focused;
        }

        internal FramedStack Owner { get; }

        internal Rect Outer { get; }

        internal string? Title { get; }

        internal bool Focused { get; }
    }
}
