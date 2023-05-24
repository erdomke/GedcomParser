﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace GedcomParser.Model
{
  public class Database
  {
    private Dictionary<string, IHasId> _nodes = new Dictionary<string, IHasId>();
    private Lookup<string, FamilyLink> _relationships = new Lookup<string, FamilyLink>();
    private Lookup<IHasId, object> _whereUsed = new Lookup<IHasId, object>();


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

    public void RemoveUnused()
    {
      var itemsToRemove = Places().Where(p => !WhereUsed(p).Any())
        .OfType<IHasId>()
        .Concat(Organizations().Where(p => !WhereUsed(p).Any()))
        .Concat(Citations().Where(p => !WhereUsed(p).Any()))
        .ToList();
      foreach (var item in itemsToRemove)
      {
        foreach (var id in item.Id)
          _nodes.Remove(id);
      }
    }

    public async Task GeocodePlaces()
    {
      var client = new HttpClient();
      client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("erdomke-GedcomParser", "1.0.0"));
      foreach (var place in Places()
        .Where(p => !p.Attributes.TryGetValue("geocoded", out var geocoded) || geocoded != "true"))
      {
        Console.WriteLine("Geocoding: " + place.Names.First().Name);
        var url = "https://nominatim.openstreetmap.org/search?format=geojson&addressdetails=1&extratags=1&q=" + Uri.EscapeDataString(place.Names.First().Name);
        var jsonString = await client.GetStringAsync(url);
        var yaml = new YamlStream();
        using (var reader = new StringReader(jsonString))
          yaml.Load(reader);
        var firstMatch = yaml.Documents[0].RootNode.Item("features").EnumerateArray().FirstOrDefault();
        place.Attributes["geocoded"] = "true";
        if (firstMatch != null)
        {
          var placeName = new PlaceName()
          {
            Name = firstMatch.Item("properties").Item("display_name").String()
          };
          var address = firstMatch.Item("properties").Item("address") as YamlMappingNode;
          if (address != null)
          {
            foreach (var part in address.Children)
            {
              switch (part.Key.String())
              {
                case "suburb":
                case "country_code":
                case "state_district":
                case "boundary":
                case "ISO3166-2-lvl4":
                case "ISO3166-2-lvl5":
                case "ISO3166-2-lvl6":
                case "ISO3166-2-lvl15":
                  break;
                default:
                  placeName.Parts.Add(new KeyValuePair<string, string>(part.Key.String(), part.Value.String()));
                  break;
              }
            }
          }
          place.Names.Insert(0, placeName);
          var wikidata = firstMatch.Item("properties").Item("extratags").Item("wikidata").String();
          if (!string.IsNullOrEmpty(wikidata))
            place.Links.Add(new Link()
            {
              Url = new Uri("https://www.wikidata.org/wiki/" + wikidata)
            });
          var wikipedia = (firstMatch.Item("properties").Item("extratags").Item("wikipedia").String() ?? "").Split(':');
          if (wikipedia.Length == 2)
            place.Links.Add(new Link()
            {
              Url = new Uri($"https://{wikipedia[0]}.wikipedia.org/wiki/{wikipedia[1].Replace(' ', '_')}")
            });
          var bbox = firstMatch.Item("bbox") as YamlSequenceNode;
          if (bbox != null)
            place.BoundingBox.AddRange(bbox.Children.Select(c => double.Parse(c.String())));
          if (firstMatch.Item("geometry").Item("type").String() == "Point")
          {
            place.Longitude = double.Parse(firstMatch.Item("geometry").Item("coordinates").Item(0).String());
            place.Latitude = double.Parse(firstMatch.Item("geometry").Item("coordinates").Item(1).String());
          }
        }
      }

      MarkDuplicates();
    }

    public void MarkDuplicates()
    {
      foreach (var group in Places().GroupBy(p =>
      {
        if (p.BoundingBox.Count > 0)
          return string.Join(",", p.BoundingBox.Select(d => d.ToString()));
        else if (p.Latitude.HasValue && p.Longitude.HasValue)
          return $"{p.Longitude.Value},{p.Latitude.Value}";
        else
          return p.Names.FirstOrDefault()?.Name.Trim() ?? Guid.NewGuid().ToString("N");
      }))
      {
        var ordered = group.OrderBy(p => p.Id.Primary).ToList();
        foreach (var place in ordered.Skip(1))
          place.DuplicateOf = ordered[0].Id.Primary;
      }
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
        foreach (var obj in group)
        {
          var newId = group.Key + (addIndex ? "_" + obj.Checksum(this).Substring(0, 5) : "");
          if (obj.Id.Add(newId, true))
            _nodes.Add(newId, obj);
        }
      }
    }

    public IEnumerable<object> WhereUsed(IHasId primaryObject)
    {
      if (_whereUsed.Count < 1)
      {
        var toProcess = _nodes.Values.OfType<object>().ToList();
        for (var i = 0; i < toProcess.Count; i++)
        {
          if (toProcess[i] is Individual individual)
          {
            foreach (var iEvent in individual.Events)
            {
              _whereUsed.Add(iEvent, individual);
            }
            foreach (var family in individual.Id
              .SelectMany(i => _relationships[i])
              .Select(l => _nodes.TryGetValue(l.Family, out var obj) ? obj : null)
              .Where(o => o != null))
              _whereUsed.Add(individual, family);
            toProcess.AddRange(individual.Names);
          }
          else if (toProcess[i] is Family family)
          {
            foreach (var fEvent in family.Events)
            {
              _whereUsed.Add(fEvent, family);
            }
          }
          else if (toProcess[i] is Event eventInfo)
          {
            if (eventInfo.Place != null)
              _whereUsed.Add(eventInfo.Place, eventInfo);
          }
          else if (toProcess[i] is Organization organization)
          {
            if (organization.Place != null)
              _whereUsed.Add(organization.Place, organization);
          }
          else if (toProcess[i] is Citation citation)
          {
            if (citation.Publisher != null)
              _whereUsed.Add(citation.Publisher, citation);
            if (citation.Repository != null)
              _whereUsed.Add(citation.Repository, citation);
          }
          else if (toProcess[i] is Place place)
          {
            toProcess.AddRange(place.Names);
          }
          else if (toProcess[i] is Media media)
          {
            if (media.Place != null)
              _whereUsed.Add(media.Place, media);
          }


          if (toProcess[i] is IHasCitations hasCitations)
          {
            foreach (var citation in hasCitations.Citations)
              _whereUsed.Add(citation, toProcess[i]);
          }

          if (toProcess[i] is IHasLinks hasLinks)
          {
            foreach (var link in hasLinks.Links)
              _whereUsed.Add(link, toProcess[i]);
          }

          if (toProcess[i] is IHasMedia hasMedia)
          {
            foreach (var media in hasMedia.Media)
              _whereUsed.Add(media, toProcess[i]);
          }

          if (toProcess[i] is IHasNotes hasNotes)
          {
            foreach (var note in hasNotes.Notes)
              _whereUsed.Add(note, toProcess[i]);
          }

          if (i == toProcess.Count - 1)
          {
            toProcess.AddRange(_whereUsed
              .Select(g => g.Key)
              .Where(o => !toProcess.Contains(o)));
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

    public IEnumerable<T> GetValues<T>()
    {
      return _nodes.Values.OfType<T>().Distinct();
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
