using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GedcomParser
{
  internal class Lookup<TKey, TElement> : ILookup<TKey, TElement>
  {
    private readonly Dictionary<TKey, Grouping<TKey, TElement>> _groups =
        new Dictionary<TKey, Grouping<TKey, TElement>>();

    public IEnumerable<TElement> this[TKey key] => _groups.TryGetValue(key, out var group) ? group : Enumerable.Empty<TElement>();

    public int Count => _groups.Count;

    public void Add(TKey key, TElement element)
    {
      if (!_groups.TryGetValue(key, out var group))
      {
        group = new Grouping<TKey, TElement>(key);
        _groups.Add(key, group);
      }
      group.Add(element);
    }

    public bool Contains(TKey key)
    {
      return _groups.ContainsKey(key);
    }

    public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator()
    {
      return _groups.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
    private class Grouping<TKey, TElement> : List<TElement>, IGrouping<TKey, TElement>
    {
      public TKey Key { get; }

      public Grouping(TKey key)
      {
        Key = key;
      }
    }
  }
}
