namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs every TUIKit descriptor sequentially through the Touchstone executor in a single
    /// NUnit test, honoring suite lifecycle hooks and preserving order.
    /// </summary>
    [TestFixture]
    public sealed class TUIKitNunitFactTests : TouchstoneNunitBase
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
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
