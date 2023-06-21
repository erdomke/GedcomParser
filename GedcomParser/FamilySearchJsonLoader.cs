using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GedcomParser
{
  internal class FamilySearchJsonLoader
  {
    public void Load(Database database, string path)
    {
      using (var stream = File.OpenRead(path))
      using (var doc = JsonDocument.Parse(stream))
      {
        Load(database, doc.RootElement);
      }
    }

    public void Load(Database database, JsonElement root)
    {
      foreach (var person in root.GetProperty("people").EnumerateObject())
      {
        if (person.Name != "UNKNOWN")
          GetCreateIndividual(database, person.Value);
      }

      foreach (var familyElement in root.GetProperty("families").EnumerateObject().Select(p => p.Value))
      {
        var id = familyElement.GetProperty("id").GetString()
          ?? familyElement.GetProperty("coupleId").GetString();
        if (database.TryGetValue<Family>(id, out var family))
          continue;
        family = new Family();
        family.Id.Add(id);
        database.Add(family);

        foreach (var property in familyElement.EnumerateObject())
        {
          switch (property.Name)
          {
            case "children":
              foreach (var child in property.Value.EnumerateArray()
                .Select(e => GetCreateIndividual(database, e.GetProperty("child")))
                .Where(i => i != null))
              {
                database.Add(new FamilyLink()
                {
                  Family = family.Id.Primary,
                  Individual = child.Id.Primary,
                  Type = FamilyLinkType.Birth
                });
              }
              break;
            case "event":
              family.Events.Add(CreateEvent(database, property.Value));
              break;
            case "parent1":
            case "parent2":
              var parent = GetCreateIndividual(database, property.Value);
              if (parent != null)
              {
                database.Add(new FamilyLink()
                {
                  Family = family.Id.Primary,
                  Individual = parent.Id.Primary,
                  Type = FamilyLinkType.Parent
                });
              }
              break;
          }
        }
      }
    }

    private Individual GetCreateIndividual(Database database, JsonElement element)
    {
      var id = element.GetProperty("id").GetString();
      if (id == "UNKNOWN")
        return null;
      if (database.TryGetValue<Individual>(id, out var individual))
        return individual;

      individual = new Individual();
      individual.Id.Add(id);
      database.Add(individual);
      individual.Links.Add(new Link()
      {
        Url = new Uri("https://www.familysearch.org/tree/person/details/" + id)
      });
      var nameString = element.GetProperty("name").GetString();
      foreach (var property in element.EnumerateObject())
      {
        switch (property.Name)
        {
          case "birth":
          case "burial":
          case "death":
            individual.Events.Add(CreateEvent(database, property.Value));
            break;
          case "gender":
            individual.Sex = Enum.Parse<Sex>(property.Value.GetString(), true);
            break;
          case "nameConclusion":
            if (property.Value.GetProperty("details").TryGetProperty("nameForms", out var nameForms))
            {
              var surname = nameForms.EnumerateArray().First().GetProperty("familyPart").GetString();
              if (!string.IsNullOrEmpty(surname))
                nameString = nameString.Replace(surname, "/" + surname + "/");
            }
            break;
          case "otherConclusions":
            foreach (var conclusion in property.Value.EnumerateArray()
              .Where(c => c.GetProperty("details").GetProperty("detailsType").GetString() == "EventDetails"))
            {
              individual.Events.Add(CreateEvent(database, conclusion));
            }
            break;
        }
      }

      if (!individual.Events.Any(e => e.Type == EventType.Birth || e.Type == EventType.Death))
      {
        if (element.TryGetProperty("fullLifespan", out var lifespan)
          || element.TryGetProperty("lifespan", out lifespan))
        {
          var parts = lifespan.GetString().Split('-');
          if (parts.Length == 2)
          {
            if (ExtendedDateRange.TryParse(parts[0].Trim(), out var birthDate))
            {
              individual.Events.Add(new Event()
              {
                Type = EventType.Birth,
                Date = birthDate
              });
            }
            if (ExtendedDateRange.TryParse(parts[1].Trim(), out var deathDate))
            {
              individual.Events.Add(new Event()
              {
                Type = EventType.Death,
                Date = deathDate
              });
            }
            else if (string.Equals(parts[1].Trim(), "Deceased", StringComparison.OrdinalIgnoreCase))
            {
              individual.Events.Add(new Event()
              {
                Type = EventType.Death
              });
            }
          }
        }
      }

      individual.Names.Add(new IndividualName()
      {
        Name = new PersonName(nameString)
      });
      return individual;
    }

    private Event CreateEvent(Database database, JsonElement element)
    {
      var details = element.GetProperty("details");
      var eventObj = new Event();
      if (Enum.TryParse<EventType>(details.GetProperty("type").GetString()?.Replace("_", "") ?? "", true, out var eventType))
      {
        eventObj.Type = eventType;
      }
      else
      {
        eventObj.Type = EventType.Generic;
        eventObj.TypeString = details.GetProperty("title").GetString()
          ?? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(details.GetProperty("type").GetString()?.Replace("_", " ").ToLowerInvariant() ?? "");
      }

      foreach (var property in details.EnumerateObject())
      {
        if (property.Value.ValueKind == JsonValueKind.Object)
        {
          switch (property.Name)
          {
            case "date":
              if (ExtendedDateRange.TryParse(property.Value.GetProperty("formalText").GetString()?.TrimStart('+') ?? "", out var date)
                || ExtendedDateRange.TryParse(property.Value.GetProperty("normalizedText").GetString(), out date))
                eventObj.Date = date;
              break;
            case "place":
              eventObj.Place = GetCreatePlace(database, property.Value);
              break;
          }
        }
      }
      return eventObj;
    }

    private Place GetCreatePlace(Database database, JsonElement element)
    {
      var id = element.GetProperty("id").GetInt32().ToString();
      if (id == "0")
        return null;
      if (database.TryGetValue<Place>(id, out var place))
        return place;
      place = new Place();
      place.Id.Add(id);
      database.Add(place);
      foreach (var property in element.EnumerateObject())
      {
        switch (property.Name)
        {
          case "geoCode":
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
              place.Latitude = property.Value.GetProperty("latitude").GetDouble();
              place.Longitude = property.Value.GetProperty("longitude").GetDouble();
            }
            break;
          case "normalizedText":
            place.Names.Add(new PlaceName()
            {
              Name = property.Value.GetString(),
            });
            break;
        }
      }
      return place;
    }
  }
}
