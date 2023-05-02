using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GedcomParser.Model
{
    public class Database
    {
        private Dictionary<string, IPrimaryObject> _nodes = new Dictionary<string, IPrimaryObject>();
        private Lookup<string, FamilyLink> _relationships = new Lookup<string, FamilyLink>();

        public void Add(IPrimaryObject primaryObject)
        {
            foreach (var id in primaryObject.Id)
                _nodes.Add(id, primaryObject);
        }

        public void Add(FamilyLink link)
        {
            _relationships.Add(link.Individual, link);
            _relationships.Add(link.Family, link);
        }

        public IEnumerable<Individual> Individuals()
        {
            return _nodes.Values.OfType<Individual>().Distinct();
        }

        public IEnumerable<Family> Families()
        {
            return _nodes.Values.OfType<Family>().Distinct();
        }

        public T GetValue<T>(string id) where T : IPrimaryObject
        {
            if (!TryGetValue(id, out T result))
                throw new InvalidOperationException($"Cannot find {typeof(T).Name} with id {id}");
            return result;
        }

        public bool TryGetValue<T>(string id, out T primary) where T : IPrimaryObject
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

        public IEnumerable<FamilyLink> FamilyLinks(IPrimaryObject primary, FamilyLinkType type)
        {
            return FamilyLinks(primary.Id.First(), type);
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
