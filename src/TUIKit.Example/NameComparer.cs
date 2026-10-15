namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;

    // Gives picker packages identity by name, so checks survive the provider building new instances.
    internal sealed class NameComparer : IEqualityComparer<PackageInfo>
    {
        public bool Equals(PackageInfo? x, PackageInfo? y)
        {
            if (x == null || y == null)
                return ReferenceEquals(x, y);

            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(PackageInfo obj)
        {
            return obj == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name);
        }
    }
}
