namespace TUIKit.Modals
{
    internal sealed class ToastClick
    {
        internal Notification Notification { get; }

        internal int ActionIndex { get; }

        internal ToastClick(Notification notification, int actionIndex)
        {
            Notification = notification;
            ActionIndex = actionIndex;
        }
    }
}
