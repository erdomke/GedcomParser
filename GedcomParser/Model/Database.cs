using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace GedcomParser.Model
{
  public class Database
  {
    private Dictionary<string, IHasId> _nodes = new Dictionary<string, IHasId>();
    private Lookup<string, FamilyLink> _relationships = new Lookup<string, FamilyLink>();
    private Lookup<IHasId, object> _whereUsed = new Lookup<IHasId, object>();

    public List<string> Roots { get; } = new List<string>();

    public List<FamilyGroup> Groups { get; } = new List<FamilyGroup>();

    public string BasePath { get; set; }

    public void Add(IHasId primaryObject)
    {
      foreach (var id in primaryObject.Id)
        _nodes.Add(id, primaryObject);
    }

    public void Add(FamilyLink link)
    {
      link.Order = _relationships[link.Family].Count();
      _relationships.Add(link.Individual, link);
      _relationships.Add(link.Family, link);
    }

    public void RemoveUnused()
    {
      var individualsToRemove = Individuals()
        .Where(i => !i.Id.SelectMany(i => _relationships[i]).Any())
        .ToList();
      foreach (var individual in individualsToRemove)
        Remove(individual);

      var familiesWithOneMember = Families()
        .Where(f => f.Id.SelectMany(i => _relationships[i]).Select(l => l.Individual).Distinct().Count() <= 1)
        .ToList();
      foreach(var family in familiesWithOneMember)
      {
        foreach (var id in family.Id)
        {
          var rels = _relationships[id].ToList();
          foreach (var rel in rels)
            _relationships.Remove(rel.Individual, rel);
          _relationships.Remove(id);
          _nodes.Remove(id);
        }
      }

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
      using (var client = new HttpClient())
      {
        client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("erdomke-GedcomParser", "1.0.0"));
        foreach (var place in Places()
          .Where(p => !p.Attributes.TryGetValue("geocoded", out var geocoded) || geocoded != "true"))
        {
          var allPlaces = await GetMatches(client, place.Names.First().Name);
          var match = allPlaces.FirstOrDefault();
          if (match != null)
          {
            place.Names.InsertRange(0, match.Names);
            foreach (var attr in match.Attributes)
              place.Attributes[attr.Key] = attr.Value;
            place.Links.AddRange(match.Links);
            if (match.BoundingBox.Count > 0)
            {
              place.BoundingBox.Clear();
              place.BoundingBox.AddRange(match.BoundingBox);
            }
            place.Latitude = match.Latitude;
            place.Longitude = match.Longitude;
          }
        }
      }

      MarkDuplicates();
    }

    private static async Task<IEnumerable<Place>> GetMatches(HttpClient client, string name)
    {
      Console.WriteLine("Geocoding: " + name);
      var url = "https://nominatim.openstreetmap.org/search?format=geojson&addressdetails=1&extratags=1&q=" + Uri.EscapeDataString(name);
      var jsonString = await client.GetStringAsync(url);
      var yaml = new YamlStream();
      using (var reader = new StringReader(jsonString))
        yaml.Load(reader);

      var places = new List<Place>();
      foreach (var match in yaml.Documents[0].RootNode.Item("features").EnumerateArray())
      {
        var place = new Place();
        places.Add(place);
        place.Attributes["geocoded"] = "true";

        var placeName = new PlaceName()
        {
          Name = match.Item("properties").Item("display_name").String()
        };
        var address = match.Item("properties").Item("address") as YamlMappingNode;
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
          if (TryGetDisplayName(placeName.Parts, out var displayName))
            placeName.Name = displayName;
        }

        place.Names.Insert(0, placeName);
        var wikidata = match.Item("properties").Item("extratags").Item("wikidata").String();
        if (!string.IsNullOrEmpty(wikidata))
          place.Links.Add(new Link()
          {
            Url = new Uri("https://www.wikidata.org/wiki/" + wikidata)
          });
        var wikipedia = (match.Item("properties").Item("extratags").Item("wikipedia").String() ?? "").Split(':');
        if (wikipedia.Length == 2)
          place.Links.Add(new Link()
          {
            Url = new Uri($"https://{wikipedia[0]}.wikipedia.org/wiki/{wikipedia[1].Replace(' ', '_')}")
          });
        var bbox = match.Item("bbox") as YamlSequenceNode;
        if (bbox != null)
          place.BoundingBox.AddRange(bbox.Children.Select(c => double.Parse(c.String())));
        if (match.Item("geometry").Item("type").String() == "Point")
        {
          place.Longitude = double.Parse(match.Item("geometry").Item("coordinates").Item(0).String());
          place.Latitude = double.Parse(match.Item("geometry").Item("coordinates").Item(1).String());
        }
      }
      return places;
    }

    private static bool TryGetDisplayName(IEnumerable<KeyValuePair<string, string>> parts, out string displayName)
    {
      var partDict = parts.ToDictionary(k => k.Key, k => k.Value, StringComparer.OrdinalIgnoreCase);
      if (partDict.TryGetValue("amenity", out var amenity)
        || partDict.TryGetValue("office", out amenity)
        || partDict.TryGetValue("building", out amenity)
        || partDict.TryGetValue("commercial", out amenity))
      {
        displayName = amenity;
        return true;
      }
      else if (partDict.TryGetValue("country", out var country))
      {
        var partList = new List<string>();
        if (partDict.TryGetValue("road", out var road))
        {
          if (partDict.TryGetValue("house_number", out var houseNumber))
            partList.Add(houseNumber + " " + road);
          else
            partList.Add(road);
        }

        if (partDict.TryGetValue("village", out var city)
          || partDict.TryGetValue("town", out city)
          || partDict.TryGetValue("city", out city))
        {
          partList.Add(city);
        }

        if (partList.Count < 1
          && partDict.TryGetValue("county", out var county))
          partList.Add(county);

        if (partDict.TryGetValue("state", out var state))
        {
          if (_stateAbbreviations.TryGetValue(state, out var abbr))
            partList.Add(abbr);
          else
            partList.Add(state);
        }

        switch (country)
        {
          case "Polska":
            partList.Add("Poland");
            break;
          case "Deutschland":
            partList.Add("Germany");
            break;
          case "United States":
            partList.Add("USA");
            break;
          case "United Kingdom":
            partList.Add("UK");
            break;
          default:
            partList.Add(country);
            break;
        }

        displayName = string.Join(", ", partList);
        return true;
      }
      else
      {
        displayName = null;
        return false;
      }
    }

    private static Dictionary<string, string> _stateAbbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      {"Alabama", "AL"},
      {"Kentucky", "KY"},
      {"Ohio", "OH"},
      {"Alaska", "AK"},
      {"Louisiana", "LA"},
      {"Oklahoma", "OK"},
      {"Arizona", "AZ"},
      {"Maine", "ME"},
      {"Oregon", "OR"},
      {"Arkansas", "AR"},
      {"Maryland", "MD"},
      {"Pennsylvania", "PA"},
      {"American Samoa", "AS"},
      {"Massachusetts", "MA"},
      {"Puerto Rico", "PR"},
      {"California", "CA"},
      {"Michigan", "MI"},
      {"Rhode Island", "RI"},
      {"Colorado", "CO"},
      {"Minnesota", "MN"},
      {"South Carolina", "SC"},
      {"Connecticut", "CT"},
      {"Mississippi", "MS"},
      {"South Dakota", "SD"},
      {"Delaware", "DE"},
      {"Missouri", "MO"},
      {"Tennessee", "TN"},
      {"District of Columbia", "DC"},
      {"Montana", "MT"},
      {"Texas", "TX"},
      {"Florida", "FL"},
      {"Nebraska", "NE"},
      {"Trust Territories", "TT"},
      {"Georgia", "GA"},
      {"Nevada", "NV"},
      {"Utah", "UT"},
      {"Guam", "GU"},
      {"New Hampshire", "NH"},
      {"Vermont", "VT"},
      {"Hawaii", "HI"},
      {"New Jersey", "NJ"},
      {"Virginia", "VA"},
      {"Idaho", "ID"},
      {"New Mexico", "NM"},
      {"Virgin Islands", "VI"},
      {"Illinois", "IL"},
      {"New York", "NY"},
      {"Washington", "WA"},
      {"Indiana", "IN"},
      {"North Carolina", "NC"},
      {"West Virginia", "WV"},
      {"Iowa", "IA"},
      {"North Dakota", "ND"},
      {"Wisconsin", "WI"},
      {"Kansas", "KS"},
      {"Northern Mariana Islands", "MP"},
      {"Wyoming", "WY"},
    };

    public void MarkDuplicates()
    {
      foreach (var group in Places().GroupBy(p =>
      {
        if (p.Attributes.TryGetValue("unique", out var unique))
          return unique;
        var result = "";
        if (p.BoundingBox.Count > 0)
          result += string.Join(",", p.BoundingBox.Select(d => d.ToString()));
        if (p.Latitude.HasValue && p.Longitude.HasValue)
          result += $"{p.Longitude.Value},{p.Latitude.Value}";
        if (string.IsNullOrEmpty(result))
          return p.Names.FirstOrDefault()?.Name.Trim() ?? Guid.NewGuid().ToString("N");
        else
          return result;
      }))
      {
        var ordered = group
          .OrderBy(p => long.TryParse(p.Id.Primary, out var lng) ? lng.ToString("D10") : p.Id.Primary)
          .ToList();
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

    public void MoveResidenceEventsToFamily()
    {
      foreach (var resolvedFamily in ResolvedFamily.Resolve(Families(), this).ToList())
      {
        foreach (var group in resolvedFamily.Events
          .Where(e => e.Event.Date.HasValue
            && e.Event.Place != null
            && (e.Event.Type == EventType.Residence || e.Event.Type == EventType.Census))
          .GroupBy(e => e.Event.Date.ToString("s") + e.Event.Place.Id.Primary)
          .Where(g => g.Skip(1).Any()))
        {
          var newEvent = CombineEvents(group.Select(e => e.Event));
          resolvedFamily.Family.Events.Add(newEvent);

          foreach (var original in group)
          {
            foreach (var individual in original.Primary)
              individual.Events.Remove(original.Event);
          }
        }
      }
    }

    public void RemoveNameOnlyIndividuals()
    {
      while (true)
      {
        var nameOnlyLeaves = _nodes.Values.OfType<Individual>()
          .Where(i => i.Attributes.Count < 1
            && i.Citations.Count < 1
            //&& i.Links.Count < 1
            && i.Media.Count < 1
            && i.Notes.Count < 1
            && i.Picture == null
            && !i.Events.Any(e => e.Attributes.Count > 0
              || e.Citations.Count > 0
              || e.Date.HasValue
              || !string.IsNullOrEmpty(e.Description)
              || e.Links.Count > 0
              || e.Media.Count > 0
              || e.Notes.Count > 0
              || e.Organization != null
              || e.Place != null)
            && !i.Id.SelectMany(i => _relationships[i]).Any(l => l.Type.HasFlag(FamilyLinkType.Child)))
          .ToList();
        if (nameOnlyLeaves.Count < 1)
          break;

        var familyParentsRemoved = new HashSet<string>();

        foreach (var individual in nameOnlyLeaves)
          familyParentsRemoved.UnionWith(Remove(individual));
        
        var danglingChildren = familyParentsRemoved
          .Where(f => _relationships[f].All(l => l.Type.HasFlag(FamilyLinkType.Child)))
          .SelectMany(f => _relationships[f]
            .Select(l => _nodes[l.Individual])
            .OfType<Individual>()
            .Where(i => i.Id.SelectMany(id => _relationships[id]).All(l => l.Family == f))
          )
          .ToList();
        foreach (var individual in danglingChildren)
          familyParentsRemoved.UnionWith(Remove(individual));
      }
    }

    public IEnumerable<string> Remove(Individual individual)
    {
      var familyIds = new List<string>();
      foreach (var id in individual.Id)
      {
        var rels = _relationships[id].ToList();
        foreach (var rel in rels)
        {
          _relationships.Remove(rel.Family, rel);
          familyIds.Add(rel.Family);
        }
        _relationships.Remove(id);
        _nodes.Remove(id);
      }
      return familyIds;
    }

    public void CombineConsecutiveResidenceEvents()
    {
      foreach (var eventParent in Families().Cast<IHasEvents>().Concat(Individuals()))
      {
        var residenceEvents = eventParent.Events
          .Where(e => e.Date.HasValue
            && e.Place != null
            && (e.Type == EventType.Residence || e.Type == EventType.Census))
          .OrderBy(e => e.Date)
          .ToList();
        foreach (var group in CombineConsecutive(residenceEvents, e => e.Place.Id.Primary)
          .Where(g => g.Skip(1).Any()))
        {
          var combined = CombineEvents(group);
          var firstDate = group.First().Date;
          var lastDate = group.Last().Date;
          combined.Date = new ExtendedDateRange(
            firstDate.Start.HasValue ? firstDate.Start : firstDate.End
            , lastDate.End.HasValue ? lastDate.End : lastDate.Start
            , DateRangeType.Range);
          eventParent.Events.Insert(eventParent.Events.IndexOf(group.First()), combined);
          foreach (var eventObj in group)
            eventParent.Events.Remove(eventObj);
        }
      }
    }

    private Event CombineEvents(IEnumerable<Event> events)
    {
      var newEvent = new Event()
      {
        Type = events.First().Type,
        Date = events.First().Date,
        Place = events.First().Place,
      };
      foreach (var attr in events.SelectMany(e => e.Attributes)
        .Distinct()
        .GroupBy(k => k.Key))
        newEvent.Attributes[attr.Key] = attr.First().Value;
      newEvent.Citations.AddRange(events.SelectMany(e => e.Citations).Distinct());
      newEvent.Links.AddRange(events.SelectMany(e => e.Links).GroupBy(l => l.Url.ToString()).Select(g => g.First()));
      newEvent.Media.AddRange(events.SelectMany(e => e.Media).GroupBy(m => m.Src ?? m.Description).Select(g => g.First()));
      newEvent.Notes.AddRange(events.SelectMany(e => e.Notes).GroupBy(n => n.Text).Select(g => g.First()));
      return newEvent;
    }

    private IEnumerable<IEnumerable<TSource>> CombineConsecutive<TSource, TKey>(IEnumerable<TSource> values, Func<TSource, TKey> keyGetter)
    {
      if (!values.Any())
        yield break;

      var curr = values.Take(1).ToList();
      var currKey = keyGetter(curr[0]);
      foreach (var value in values.Skip(1))
      {
        var newKey = keyGetter(value);
        if (newKey.Equals(currKey))
        {
          curr.Add(value);
        }
        else
        {
          yield return curr;
          curr = new List<TSource>() { value };
          currKey = newKey;
        }
      }
      yield return curr;
    }

    private void UpdateIndices<T>(IEnumerable<T> objects) where T : IHasId
    {
      foreach (var group in objects.GroupBy(i => i.GetPreferredId(this), StringComparer.OrdinalIgnoreCase))
      {
        var addIndex = group.Skip(1).Any();
        foreach (var obj in group)
        {
          var count = 5;
          var newId = group.Key + (addIndex ? "_" + obj.Checksum(this).Substring(0, count) : "");
          while (addIndex && _nodes.ContainsKey(newId))
          {
            count++;
            newId = group.Key + "_" + obj.Checksum(this).Substring(0, count);
          }
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
            if (individual.Picture != null)
            {
              _whereUsed.Add(individual.Picture, individual);
              toProcess.Add(individual.Picture);
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

    public IEnumerable<Individual> Siblings(Individual individual)
    {
      return FamilyLinks(individual, FamilyLinkType.Birth)
        .SelectMany(l => FamilyLinks(l.Family, FamilyLinkType.Birth))
        .OrderBy(l => l.Order)
        .Select(l => TryGetValue(l.Individual, out Individual sibling) ? sibling : null)
        .Where(s => s != null);
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
  }
}
