namespace TUIKit.Layout
{
    /// <summary>
    /// How a <see cref="StackSize"/> sizes one child of a stack along the stack's axis.
    /// </summary>
    public enum StackSizeKind
    {
        /// <summary>Exactly <see cref="StackSize.Length"/> rows or columns.</summary>
        Fixed = 0,

        /// <summary>
        /// A share of the space left after fixed children, in proportion to <see cref="StackSize.Weight"/>,
        /// never less than <see cref="StackSize.Minimum"/>.
        /// </summary>
        Weight = 1
    }
}
