using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{DebuggerDisplay,nq}")]
  public class Identifiers : IEnumerable<string>
  {
    private readonly HashSet<string> _ids = new HashSet<string>();

    private string DebuggerDisplay => string.Join(", ", _ids);

    public Identifiers() { }

    public Identifiers(string id)
    {
      _ids.Add(id);
    }

    public void Add(string id)
    {
      _ids.Add(id);
    }

    public void AddRange(IEnumerable<string> identifiers)
    {
      _ids.UnionWith(identifiers);
    }

    public bool Contains(string identifier)
    {
      return _ids.Contains(identifier);
    }

    public bool Overlaps(IEnumerable<string> identifiers)
    {
      return _ids.Overlaps(identifiers);
    }

    public IEnumerator<string> GetEnumerator()
    {
      return _ids.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}
