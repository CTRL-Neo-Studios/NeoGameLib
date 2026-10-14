using System;
using System.Buffers;
using System.Collections.Generic;

namespace NeoGameLib.Common;

/// <summary>
/// A wrapper that helps with iterating loops, with functions that helps process removal-during-iteration or addition-during-iteration.
/// </summary>
/// <remarks>
/// <para>The problem we have before that is existent in EACH LIFECYCLE of NeoGameLib is that a list can be mutated during iteration.</para>
/// <para>For example: while iterating through a game object list, a component from a game object could: 1. kill a game object in that list; 2. add a new game object ot that list; 3. kill every game object in that list.</para>
/// <para>What results from that example:</para>
/// <list type="number">
/// <item><description>a "Collection was modified" runtime exception that explodes the game;</description></item>
/// <item><description>skipped items -- an item at index 3 was removed while the loop is still iterating on index 3, and index 4 skips to index 3 -- causing the original index 4-but-now-3-object skipped, game explodes;</description></item>
/// <item><description>An object was destroyed already but its update function was still called in the next tick because indexes are stale, game explodes</description></item>
/// </list>
/// <para>
/// There are easy micro fixes to prevent some of the issues, i.e. a reverse-for-loop can solve the removal-during-iteration but not the addition issue; list.ToArray() generates fucking garbage every fucking frame which will slow down the game tick loop a lot;
/// snapshotting into a list which shares the same GC problem; locking/deferring modifications to a queue which requires more mental power for me to implement and it's way too inconvenient to keep track of every single callback.
/// </para>
/// <para>So this struct was created to address all of the above. Mechanically, it does 4 things:</para>
/// <list type="number">
/// <item><description>Copies the list at the start of the pass into a shared array, which prevents modification of the list since user code modifies the real list, not the copied list. Everything the user code modifies to the real list is then snapshotted in the next frame and then ran.</description></item>
/// <item><description>Uses the Shared Array Pool instead of creating a new list instance every time. The <c>ArrayPool&lt;T&gt;.Shared</c> gives a pre-existing array so there's no new-list-creating and no GC required for creating a list.</description></item>
/// <item><description>Clears its own pool for stale references (i.e. dead objects, etc.) before returning it back</description></item>
/// <item><description>Each call gets its own snapshot: for example, if an enemy update func needs to iterate through another list i.e. collisions list or its "children", the inner loop rents its own array. Simpler words: nesting works.</description></item>
/// </list>
/// <para>Some caveats while using this:</para>
/// <list type="bullet">
/// <item><description>You must check <c>IsAttached</c> or its equivalent in the loop iteration. Since the snapshot won't know that an item was destroyed after it's copied.</description></item>
/// <item><description>"Removed" means "detached from array", but not null in the snapshot. You'll decide whether that reference is still alive.</description></item>
/// <item><description>Count is readonly. (duh)</description></item>
/// <item><description>If the list is empty, it'll still rent a 0-length array. But this is much better than creating a list on each update tick so a very efficient compromise.</description></item>
/// </list>
/// </remarks>
public readonly struct PassSnapshot<T> : IDisposable
{
    private readonly T[] _items;
    public readonly int Count;

    public PassSnapshot(IReadOnlyList<T> list)
    {
        Count = list.Count;
        _items = ArrayPool<T>.Shared.Rent(Math.Max(Count, 1));
        for (int i = 0; i < Count; i++) _items[i] = list[i];
    }

    public T this[int index] => _items[index];

    public void Dispose()
    {
        // clear so the pool doesn't keep destroyed objects alive
        Array.Clear(_items, 0, Count);
        ArrayPool<T>.Shared.Return(_items);
    }
}
