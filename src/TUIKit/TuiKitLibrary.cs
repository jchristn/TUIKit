namespace TUIKit
{
    using System.Reflection;

    /// <summary>
    /// Provides assembly-level metadata for the TUIKit library. All members are thread-safe.
    /// </summary>
    public static class TuiKitLibrary
    {
        /// <summary>
        /// Gets the human-readable name of the library. Never null or empty.
        /// </summary>
        public static string Name
        {
            get { return "TUIKit"; }
        }

        /// <summary>
        /// Gets the informational version string of the loaded library assembly. Falls back to
        /// the assembly version when no informational version attribute is present. Never null.
        /// </summary>
        public static string Version
        {
            get
            {
                Assembly assembly = typeof(TuiKitLibrary).Assembly;
                AssemblyInformationalVersionAttribute? informational =
                    assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

                if (informational != null && !string.IsNullOrEmpty(informational.InformationalVersion))
                    return informational.InformationalVersion;

                AssemblyName name = assembly.GetName();
                return name.Version != null ? name.Version.ToString() : "0.0.0";
            }
        }
    }
}
