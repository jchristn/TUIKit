namespace TUIKit.Compatibility
{
    using System;
    using System.Diagnostics;

    internal static class RuntimeCompat
    {
        internal static void ThrowIfNull(object? value, string parameterName)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(value, parameterName);
#else
            if (value == null)
                throw new ArgumentNullException(parameterName);
#endif
        }

        internal static void KillProcessTree(Process process)
        {
#if NET8_0_OR_GREATER
            process.Kill(entireProcessTree: true);
#else
            process.Kill();
#endif
        }
    }
}
