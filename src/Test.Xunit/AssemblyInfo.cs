// Tests exercise the TuiApplication singleton (the terminal is a global resource), so the Fact and
// Theory fixtures must not run concurrently.
[assembly: global::Xunit.CollectionBehavior(DisableTestParallelization = true)]
