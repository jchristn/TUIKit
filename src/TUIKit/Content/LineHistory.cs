namespace TUIKit.Content
{
    using System.Collections.Generic;

    // Indexes are logical. Eviction releases each line immediately; compaction waits
    // until at least half of the backing list is unused. The owning pane synchronizes access.
    internal sealed class LineHistory
    {
        private readonly List<PaneLine?> _Items = new List<PaneLine?>();
        private int _Head;

        internal int Count => _Items.Count - _Head;

        internal PaneLine this[int index] => _Items[_Head + index]!;

        internal void Add(PaneLine line) => _Items.Add(line);

        internal void Clear()
        {
            _Items.Clear();
            _Head = 0;
        }

        internal void RemoveAt(int index)
        {
            if (index != 0)
            {
                _Items.RemoveAt(_Head + index);
                return;
            }

            _Items[_Head++] = null;
            if (_Head >= Count)
            {
                _Items.RemoveRange(0, _Head);
                _Head = 0;
            }
        }
    }
}
