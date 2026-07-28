namespace TUIKit.Layout
{
    /// <summary>
    /// How a region's position and length along one axis respond to changes in the surface size.
    /// </summary>
    public enum AxisMode
    {
        /// <summary>
        /// Fixed offset from the start edge and fixed length. The region does not scale and stays
        /// anchored to the start (left or top) edge.
        /// </summary>
        Fixed = 0,

        /// <summary>
        /// Fixed length anchored to the end edge (right or bottom); the offset tracks the edge as the
        /// surface grows or shrinks.
        /// </summary>
        FromEnd = 1,

        /// <summary>
        /// The region fills the space between a start pad and an end pad, growing and shrinking with
        /// the surface.
        /// </summary>
        Stretch = 2,

        /// <summary>
        /// The offset and length are fractions of the surface size, so the region scales
        /// proportionally.
        /// </summary>
        Proportional = 3
    }
}
