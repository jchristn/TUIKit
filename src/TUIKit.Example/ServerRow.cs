namespace TUIKit.Example
{
    // One row of the simulated server behind the data tour page.
    internal sealed class ServerRow
    {
        internal ServerRow(int id, string name, string status, int sizeKb)
        {
            Id = id;
            Name = name;
            Status = status;
            SizeKb = sizeKb;
        }

        internal int Id { get; }

        internal string Name { get; }

        internal string Status { get; }

        internal int SizeKb { get; }
    }
}
