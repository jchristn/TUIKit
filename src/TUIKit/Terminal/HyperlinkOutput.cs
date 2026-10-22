namespace TUIKit.Terminal
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    internal static class HyperlinkOutput
    {
        internal static void Validate(Uri uri)
        {
            if (!uri.IsAbsoluteUri || HasControl(uri.OriginalString)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps
                    && uri.Scheme != Uri.UriSchemeFile && uri.Scheme != Uri.UriSchemeMailto))
                throw new ArgumentException("Hyperlinks require an absolute HTTP, HTTPS, file, or mailto URI without control characters.", nameof(uri));
        }

        internal static bool HasControl(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i]))
                    return true;
            }
            return false;
        }

        internal static Uri? TryTarget(string? value)
        {
            if (value == null || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
                return null;
            try
            {
                Validate(uri);
                return uri;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        internal static void Transition(StringBuilder builder, ref string? current, Uri? target)
        {
            string? next = target?.AbsoluteUri;
            if (string.Equals(current, next, StringComparison.Ordinal))
                return;
            if (current != null)
                builder.Append(Ansi.CloseHyperlink);
            if (next != null)
            {
                using (SHA256 hash = SHA256.Create())
                {
                    string id = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(next))).Replace("-", string.Empty);
                    builder.Append(Ansi.OpenHyperlink(id, next));
                }
            }
            current = next;
        }

        internal static bool Enabled(HyperlinkMode mode, bool supported)
        {
            return mode == HyperlinkMode.Always || (mode == HyperlinkMode.Auto && supported);
        }
    }
}
