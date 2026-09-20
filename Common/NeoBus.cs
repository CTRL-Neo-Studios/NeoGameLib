using System.Collections.Generic;

namespace NeoGameLib.Common;

// note to future self:
// basically what all of the busses we've has. while some buses may have more than one item, for now for simplicity's sake just stick with one.
// you'll probably thank me for later, who knows.
public abstract class NeoBus<T>
{
    protected List<T> Items = new();

    public void Add(T item)
    {
        if (Items.Contains(item)) return;
        Items.Add(item);
    }

    public void Remove(T item)
    {
        Items.Remove(item);
    }
}
