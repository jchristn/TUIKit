namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;

    /// <summary>
    /// Touchstone suite covering the <see cref="Point"/>, <see cref="Size"/>, and <see cref="Rect"/>
    /// geometry primitives.
    /// </summary>
    public static class GeometrySuite
    {
        /// <summary>
        /// Builds the geometry suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Geometry",
                displayName: "Geometry Primitives",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Geometry", "PointEquality", "Point value equality and offset",
                        _ =>
                        {
                            Check.True(new Point(2, 3) == new Point(2, 3), "Equal points must compare equal.");
                            Check.True(new Point(2, 3).Offset(1, -1) == new Point(3, 2), "Offset must translate.");
                            Check.False(new Point(0, 0) == new Point(1, 0), "Different points must not be equal.");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Geometry", "SizeNegativeThrows", "Negative size dimensions throw",
                        _ =>
                        {
                            Check.Throws<System.ArgumentOutOfRangeException>(
                                () => new Size(-1, 5), "Negative width");
                            Check.Throws<System.ArgumentOutOfRangeException>(
                                () => new Size(5, -1), "Negative height");
                            Check.True(new Size(0, 5).IsEmpty, "Zero width is empty.");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Geometry", "RectContains", "Rect contains uses exclusive edges",
                        _ =>
                        {
                            Rect rect = new Rect(1, 1, 3, 2);
                            Check.True(rect.Contains(new Point(1, 1)), "Top-left corner is inside.");
                            Check.True(rect.Contains(new Point(3, 2)), "Last interior cell is inside.");
                            Check.False(rect.Contains(new Point(4, 1)), "Right edge is exclusive.");
                            Check.False(rect.Contains(new Point(1, 3)), "Bottom edge is exclusive.");
                            Check.Equal(4, rect.Right, "Right");
                            Check.Equal(3, rect.Bottom, "Bottom");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Geometry", "RectIntersect", "Rect intersection and disjoint result",
                        _ =>
                        {
                            Rect a = new Rect(0, 0, 4, 4);
                            Rect b = new Rect(2, 2, 4, 4);
                            Check.Equal(new Rect(2, 2, 2, 2), a.Intersect(b), "Overlap");
                            Check.True(a.Intersect(new Rect(10, 10, 2, 2)).IsEmpty, "Disjoint yields empty.");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
