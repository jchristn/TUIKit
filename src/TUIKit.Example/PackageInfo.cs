namespace TUIKit.Example
{
    // One row of the picker demo: a package with a version, a description, and whether it can be installed.
    internal sealed class PackageInfo
    {
        internal PackageInfo(string name, string version, string description, bool available)
        {
            Name = name;
            Version = version;
            Description = description;
            Available = available;
        }

        internal string Name { get; }

        internal string Version { get; }

        internal string Description { get; }

        internal bool Available { get; }
    }
}
