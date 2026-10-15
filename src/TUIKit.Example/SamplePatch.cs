namespace TUIKit.Example
{
    // The bundled three-file patch shown by the "Diffs from git" tour page and --diff-once. It covers a modified
    // file, a new file, a rename, and a removed line whose text starts with "--", which a naive parser would take
    // for a file header.
    internal static class SamplePatch
    {
        internal const string Text =
            "diff --git a/src/Server/Config.cs b/src/Server/Config.cs\n" +
            "index 3f2a9c1..8b7d0e4 100644\n" +
            "--- a/src/Server/Config.cs\n" +
            "+++ b/src/Server/Config.cs\n" +
            "@@ -12,7 +12,8 @@ public sealed class Config\n" +
            "     public string Host { get; set; } = \"localhost\";\n" +
            "-    public int Port { get; set; } = 80;\n" +
            "+    public int Port { get; set; } = 8080;\n" +
            "+    public bool UseTls { get; set; } = true;\n" +
            "     public int Workers { get; set; } = 4;\n" +
            "     public string LogLevel { get; set; } = \"info\";\n" +
            "     public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);\n" +
            "     public bool Verbose { get; set; }\n" +
            "diff --git a/db/migrations/0042_jobs.sql b/db/migrations/0042_jobs.sql\n" +
            "new file mode 100644\n" +
            "index 0000000..a1b2c3d\n" +
            "--- /dev/null\n" +
            "+++ b/db/migrations/0042_jobs.sql\n" +
            "@@ -0,0 +1,5 @@\n" +
            "+-- jobs table for the scheduler\n" +
            "+CREATE TABLE jobs (\n" +
            "+    id INTEGER PRIMARY KEY,\n" +
            "+    name TEXT NOT NULL\n" +
            "+);\n" +
            "diff --git a/docs/setup.md b/docs/guide/setup.md\n" +
            "similarity index 88%\n" +
            "rename from docs/setup.md\n" +
            "rename to docs/guide/setup.md\n" +
            "index 9c8d7e6..1f2e3d4 100644\n" +
            "--- a/docs/setup.md\n" +
            "+++ b/docs/guide/setup.md\n" +
            "@@ -1,4 +1,4 @@\n" +
            " # Setup\n" +
            "--- old note: run as root\n" +
            "+Run as an unprivileged user.\n" +
            " Install the package, then start the service.\n" +
            " See the configuration guide for ports and TLS.\n" +
            "\\ No newline at end of file\n";
    }
}
