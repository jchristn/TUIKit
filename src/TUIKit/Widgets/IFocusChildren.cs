namespace TUIKit.Widgets
{
    internal interface IFocusChildren
    {
        bool HasFocusableChild { get; }

        bool HasTabStopChild { get; }
    }
}
