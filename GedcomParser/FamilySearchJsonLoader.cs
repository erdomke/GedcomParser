using GedcomParser.Model;
using System;
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
      var primaryName = new IndividualName()
      {
        Name = new PersonName(element.GetProperty("name").GetString())
      };
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
          case "lifeSketch":
            var sketchMedia = new Media()
            {
              Content = property.Value.GetProperty("details").GetProperty("text").GetString()
            };
            individual.Media.Add(sketchMedia);
            break;
          case "nameConclusion":
            primaryName = CreateName(property.Value);
            break;
          case "otherConclusions":
            foreach (var conclusion in property.Value.EnumerateArray())
            {
              switch (conclusion.GetProperty("details").GetProperty("detailsType").GetString())
              {
                case "EventDetails":
                  individual.Events.Add(CreateEvent(database, conclusion));
                  break;
                case "NameDetails":
                  var newName = CreateName(conclusion);
                  if (newName.Name.Name != primaryName.Name.Name
                    && !primaryName.Name.Name.Contains(newName.Name.Name, StringComparison.InvariantCultureIgnoreCase))
                    individual.Names.Add(newName);
                  break;
              }
            }
            break;
          case "portraitUrl":
            if (!string.IsNullOrEmpty(property.Value.GetString()))
            {
              individual.Picture = new Media()
              {
                Src = property.Value.GetString()
              };
            }
            break;
          case "memories":
            foreach (var memory in property.Value.EnumerateArray())
            {
              if (memory.TryGetProperty("url", out var urlProp))
              {
                var media = new Media()
                {
                  Src = urlProp.GetString()
                };
                if (memory.TryGetProperty("mimeType", out var mimeTypeProp))
                  media.MimeType = mimeTypeProp.GetString();
                if (memory.TryGetProperty("height", out var heightProp)
                  && memory.TryGetProperty("width", out var widthProp))
                {
                  media.Height = heightProp.GetInt32();
                  media.Width = widthProp.GetInt32();
                }
                if ((memory.TryGetProperty("description", out var titleProp) && !string.IsNullOrEmpty(titleProp.GetString()))
                  || (memory.TryGetProperty("title", out titleProp) && !string.IsNullOrEmpty(titleProp.GetString()))
                  || (memory.TryGetProperty("originalFilename", out titleProp) && !string.IsNullOrEmpty(titleProp.GetString())))
                  media.Description = titleProp.GetString();
                individual.Media.Add(media);
              }
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

      individual.Names.Insert(0, primaryName);
      return individual;
    }

    private IndividualName CreateName(JsonElement element)
    {
      var nameString = element.GetProperty("details").GetProperty("fullText").GetString();
      var name = new IndividualName();
      if (element.GetProperty("details").TryGetProperty("nameForms", out var nameForms))
      {
        foreach (var property in nameForms.EnumerateArray().First().EnumerateObject())
        {
          switch (property.Name)
          {
            case "familyPart":
              var surname = property.Value.GetString();
              if (!string.IsNullOrEmpty(surname))
              {
                nameString = nameString.Replace(surname, "/" + surname + "/");
                name.Surname = surname;
              }
              break;
            case "givenPart":
              name.GivenName = property.Value.GetString();
              break;
            case "prefixPart":
              name.NamePrefix = property.Value.GetString();
              break;
            case "suffixPart":
              name.NameSuffix = property.Value.GetString();
              break;
          }
        }
      }
      if (Enum.TryParse<NameType>(element.GetProperty("details").GetProperty("nameType").GetString(), true, out var type))
        name.Type = type;
      name.Name = new PersonName(nameString);
      return name;
    }

    private Event CreateEvent(Database database, JsonElement element)
    {
      var details = element.GetProperty("details");
      var eventObj = new Event();
      var type = details.GetProperty("type").GetString()?.Replace("_", "");
      if (type == "OTHEREVENT")
        type = details.GetProperty("title").GetString();
      if (Enum.TryParse<EventType>(type ?? "", true, out var eventType))
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

      if (element.TryGetProperty("justification", out var justification)
        && !string.IsNullOrEmpty(justification.GetString()))
      {
        if (justification.GetString() != "GEDCOM data")
        {
          if (Uri.TryCreate(justification.GetString(), UriKind.Absolute, out var uri))
            eventObj.Links.Add(new Link() { Url = uri });
          else
            eventObj.Notes.Add(new Note() { Text = justification.GetString() });
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
