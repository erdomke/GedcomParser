using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{DebuggerDisplay,nq}")]
  public class Identifiers : IEnumerable<string>
  {
    private readonly HashSet<string> _ids = new HashSet<string>();

    public string Primary { get; set; }

    private string DebuggerDisplay => string.Join(", ", _ids);

    public Identifiers() { }

    public Identifiers(string id)
    {
      Add(id);
    }

    public void Add(string id)
    {
      Add(id, string.IsNullOrEmpty(Primary));
    }

    public bool Add(string id, bool primary)
    {
      if (!string.IsNullOrEmpty(id))
      {
        var result = _ids.Add(id);
        if (primary)
          Primary = id;
        return result;
      }
      else
      {
        return false;
      }
    }

    public void AddRange(IEnumerable<string> identifiers)
    {
      if (string.IsNullOrEmpty(Primary))
        Primary = identifiers.FirstOrDefault();
      _ids.UnionWith(identifiers.Where(i => !string.IsNullOrEmpty(i)));
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

    public override string ToString()
    {
      return Primary;
    }
  }
}
