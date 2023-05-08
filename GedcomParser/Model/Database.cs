using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GedcomParser.Model
{
  public class Database
  {
    private Dictionary<string, IHasId> _nodes = new Dictionary<string, IHasId>();
    private Lookup<string, FamilyLink> _relationships = new Lookup<string, FamilyLink>();
    private Lookup<IHasId, IHasId> _whereUsed = new Lookup<IHasId, IHasId>();


    public void Add(IHasId primaryObject)
    {
      foreach (var id in primaryObject.Id)
        _nodes.Add(id, primaryObject);
    }

    public void Add(FamilyLink link)
    {
      _relationships.Add(link.Individual, link);
      _relationships.Add(link.Family, link);
    }

    public void MakeIdsHumanReadable()
    {
      UpdateIndices(Citations());
      UpdateIndices(Families());
      UpdateIndices(Individuals());
      UpdateIndices(Organizations());
      UpdateIndices(Places());
    }

    private void UpdateIndices<T>(IEnumerable<T> objects) where T : IHasId
    {
      foreach (var group in objects.GroupBy(i => i.GetPreferredId(this), StringComparer.OrdinalIgnoreCase))
      {
        var addIndex = group.Skip(1).Any();
        var i = 0;
        foreach (var obj in group)
        {
          var newId = group.Key + (addIndex ? i.ToString("D2") : "");
          if (obj.Id.Add(newId, true))
            _nodes.Add(newId, obj);
          i++;
        }
      }
    }

    public IEnumerable<IHasId> WhereUsed(IHasId primaryObject)
    {
      if (_whereUsed.Count < 1)
      {
        var toProcess = _nodes.Values.ToList();
        for (var i = 0; i < toProcess.Count; i++)
        {
          if (toProcess[i] is Individual individual)
          {
            foreach (var iEvent in individual.Events)
            {
              _whereUsed.Add(iEvent, individual);
              if (!toProcess.Contains(iEvent))
                toProcess.Add(iEvent);
            }
            foreach (var family in individual.Id
              .SelectMany(i => _relationships[i])
              .Select(l => _nodes.TryGetValue(l.Family, out var obj) ? obj : null)
              .Where(o => o != null))
              _whereUsed.Add(individual, family);
          }
          else if (toProcess[i] is Family family)
          {
            foreach (var fEvent in family.Events)
            {
              _whereUsed.Add(fEvent, family);
              if (!toProcess.Contains(fEvent))
                toProcess.Add(fEvent);
            }
          }
          else if (toProcess[i] is Event eventInfo)
          {
            if (eventInfo.Place != null)
              _whereUsed.Add(eventInfo.Place, eventInfo);
            foreach (var obj in eventInfo.Notes)
              _whereUsed.Add(obj, eventInfo);
            foreach (var obj in eventInfo.Citations)
              _whereUsed.Add(obj, eventInfo);
          }
        }
      }
      return _whereUsed[primaryObject];
    }

    public IEnumerable<Citation> Citations()
    {
      return _nodes.Values.OfType<Citation>().Distinct();
    }

    public IEnumerable<Individual> Individuals()
    {
      return _nodes.Values.OfType<Individual>().Distinct();
    }

    public IEnumerable<Family> Families()
    {
      return _nodes.Values.OfType<Family>().Distinct();
    }

    public IEnumerable<Organization> Organizations()
    {
      return _nodes.Values.OfType<Organization>().Distinct();
    }

    public IEnumerable<Place> Places()
    {
      return _nodes.Values.OfType<Place>().Distinct();
    }

    public T GetValue<T>(string id) where T : IHasId
    {
      if (!TryGetValue(id, out T result))
        throw new InvalidOperationException($"Cannot find {typeof(T).Name} with id {id}");
      return result;
    }

    public bool TryGetValue<T>(string id, out T primary) where T : IHasId
    {
      if (_nodes.TryGetValue(id, out var primaryObject)
          && primaryObject is T typed)
      {
        primary = typed;
        return true;
      }
      else
      {
        primary = default;
        return false;
      }
    }

    public IEnumerable<FamilyLink> FamilyLinks(IHasId primary, FamilyLinkType type)
    {
      return primary.Id.SelectMany(i => FamilyLinks(i, type));
    }

    public IEnumerable<FamilyLink> FamilyLinks(string id, FamilyLinkType type)
    {
      return _relationships[id]
        .Where(f => (f.Type & type) == type)
        .OrderBy(f => f.Type);
    }

    public IEnumerable<IndividualLink> IndividualLinks(string id, FamilyLinkType type1, FamilyLinkType type2)
    {
      foreach (var link1 in FamilyLinks(id, type1))
      {
        foreach (var link2 in FamilyLinks(link1.Family, type2))
        {
          yield return new IndividualLink()
          {
            Individual1 = id,
            LinkType1 = link1.Type,
            Individual2 = link2.Individual,
            LinkType2 = link2.Type
          };
        }
      }
    }

    private class Lookup<TKey, TElement> : ILookup<TKey, TElement>
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
