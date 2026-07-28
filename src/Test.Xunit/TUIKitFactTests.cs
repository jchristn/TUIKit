namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// Runs every TUIKit descriptor sequentially through the Touchstone executor in a single
    /// xUnit fact, honoring suite lifecycle hooks and preserving order.
    /// </summary>
    public sealed class TUIKitFactTests : TouchstoneFactBase
    {
        /// <summary>
        /// Gets the suites executed by this fixture.
        /// </summary>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return TUIKitSuites.All; }
        }

        /// <summary>
        /// Executes all TUIKit suites and fails if any descriptor fails.
        /// </summary>
        /// <returns>A task that completes when all suites have run.</returns>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
