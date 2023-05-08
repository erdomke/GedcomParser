using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class GrampsXmlLoader
  {
    private static XNamespace grampsNs = "http://gramps-project.org/xml/1.7.1/";
    private Dictionary<string, Note> _importNotes = new Dictionary<string, Note>();

    private void BuildSchema(XElement element, string path, HashSet<string> paths)
    {
      var name = element.Name.LocalName;
      var attrNames = string.Join(",", element.Attributes().Select(a => "@" + a.Name));
      if (!string.IsNullOrEmpty(attrNames))
        name += "[" + attrNames + "]";
      if (element.HasElements)
      {
        foreach (var child in element.Elements())
        {
          BuildSchema(child, path + "/" + name, paths);
        }
      }
      else
      {
        paths.Add(path + "/" + name);
      }
    }

    private Dictionary<string, int> _addressPartsOrder = new Dictionary<string, int>()
    {
      { "street", 0 },
      { "city", 1 },
      { "locality", 2 },
      { "state", 3 },
      { "country", 4 },
    };

    public void Load(Database database, XElement root)
    {
      foreach (var noteElement in root.Element(grampsNs + "notes").Elements(grampsNs + "note"))
      {
        var note = new Note()
        {
          Text = (string)noteElement.Element(grampsNs + "text")
        };
        AddCommonFields(noteElement, note, database);
        if ((string)noteElement.Attribute("type") == "GEDCOM import")
          _importNotes.Add(note.Id.Last(), note);
        else
          database.Add(note);
      }

      foreach (var repositoryElement in root.Element(grampsNs + "repositories").Elements(grampsNs + "repository"))
        database.Add(CreateRepository(repositoryElement, database));

      foreach (var group in root.Element(grampsNs + "citations").Elements(grampsNs + "citation")
        .Select(e => CreateCitation(e, root, database))
        .GroupBy(c => c))
      {
        var citation = group.First();
        citation.Id.AddRange(group.Skip(1).SelectMany(c => c.Id));
        if (citation.TryGetLink(out var link))
          database.Add(link);
        else
          database.Add(citation);
      }
      
      foreach (var objElement in root.Element(grampsNs + "objects").Elements(grampsNs + "object"))
      {
        var obj = new Media();
        AddCommonFields(objElement, obj, database);
        var file = objElement.Element(grampsNs + "file");
        if (file != null)
        {
          obj.Src = (string)file.Attribute("src");
          obj.MimeType = (string)file.Attribute("mime");
          obj.Description = (string)file.Attribute("description");
          if (obj.Description == Path.GetFileNameWithoutExtension(obj.Src))
            obj.Description = null;
        }
        if (TryGetDate(objElement, out var date))
          obj.Date = date;
        database.Add(obj);
      }

      foreach (var placeElement in root.Element(grampsNs + "places").Elements(grampsNs + "placeobj"))
        database.Add(CreatePlace(placeElement, database));
      
      foreach (var eventElement in root.Element(grampsNs + "events").Elements(grampsNs + "event"))
        database.Add(CreateEvent(eventElement, database));
      
      foreach (var individualElement in root.Element(grampsNs + "people").Elements(grampsNs + "person"))
      {
        var individual = new Individual();
        AddCommonFields(individualElement, individual, database);
        if ((string)individualElement.Attribute("gender") == "M")
          individual.Sex = Sex.Male;
        if ((string)individualElement.Attribute("gender") == "F")
          individual.Sex = Sex.Female;
        foreach (var name in individualElement.Elements(grampsNs + "name"))
        {
          var iName = new IndividualName();
          AddCommonFields(name, iName, database);
          switch (((string)name.Attribute("type")).ToUpperInvariant())
          {
            case "ALSO KNOWN AS":
              iName.Type = NameType.Aka;
              break;
            case "BIRTH NAME":
              iName.Type = NameType.Birth;
              break;
            case "MARRIED NAME":
              iName.Type = NameType.Married;
              break;
            //case "UNKNOWN":
            //case "OTHER NAME":
            default:
              iName.Type = NameType.Other;
              break;
          }
          iName.NamePrefix = (string)name.Element(grampsNs + "title");
          iName.GivenName = (string)name.Element(grampsNs + "first");
          iName.Nickname = (string)name.Element(grampsNs + "nick");
          iName.SurnamePrefix = (string)name.Element(grampsNs + "surname")?.Attribute("prefix");
          iName.Surname = (string)name.Element(grampsNs + "surname");
          iName.NameSuffix = (string)name.Element(grampsNs + "suffix");

          iName.Name = new PersonName(string.Join(" ", new[] { 
            iName.NamePrefix, 
            iName.GivenName, 
            iName.Nickname, 
            iName.SurnamePrefix, 
            string.IsNullOrEmpty(iName.Surname) ? null : "/" + iName.Surname + "/",
            iName.NameSuffix 
          }.Where(n => !string.IsNullOrEmpty(n))));
          individual.Names.Add(iName);
        }

        var events = new List<Event>();
        foreach (var eventRef in individualElement.Elements(grampsNs + "eventref"))
        {
          if (database.TryGetValue((string)eventRef.Attribute("hlink"), out Event eventObj))
            events.Add(eventObj);
        }

        foreach (var addressRef in individualElement.Elements(grampsNs + "address"))
        {
          var eventObj = new Event
          {
            Type = EventType.Residence,
            Place = new Place()
          };
          AddCommonFields(addressRef, eventObj, database);
          eventObj.Place.Id.Add(Guid.NewGuid().ToString("N"));
          eventObj.Place.Names.Add(string.Join(", ", addressRef.Elements()
            .Where(e => _addressPartsOrder.ContainsKey(e.Name.LocalName))
            .OrderBy(e => _addressPartsOrder[e.Name.LocalName])
            .Select(e => (string)e)));
          database.Add(eventObj.Place);
          if (TryGetDate(addressRef, out var date))
            eventObj.Date = date;
          events.Add(eventObj);
        }

        individual.Events.AddRange(events
          .OrderBy(e =>
          {
            if (e.Type == EventType.Birth)
              return 0;
            if (e.Type == EventType.Death)
              return 10;
            if (e.Type == EventType.Burial
              || e.Type == EventType.Probate)
              return 11;
            return 5;
          })
          .ThenBy(e => e.Date.ToString("s")));

        database.Add(individual);
      }

      foreach (var familyElement in root.Element(grampsNs + "families").Elements(grampsNs + "family"))
      {
        var family = new Family();
        AddCommonFields(familyElement, family, database);
        if (Enum.TryParse(((string)familyElement.Element(grampsNs + "rel")?.Attribute("type") ?? "Unknown").Replace(" ", ""), true, out FamilyType familyType))
          family.Type = familyType;
        foreach (var eventRef in familyElement.Elements(grampsNs + "eventref"))
        {
          if (database.TryGetValue((string)eventRef.Attribute("hlink"), out Event eventObj))
            family.Events.Add(eventObj);
        }
        foreach (var famRel in familyElement.Elements(grampsNs + "father")
          .Concat(familyElement.Elements(grampsNs + "mother"))
          .Concat(familyElement.Elements(grampsNs + "childref")))
        {
          var type = default(FamilyLinkType);
          if (famRel.Name.LocalName == "father")
            type = FamilyLinkType.Father;
          else if (famRel.Name.LocalName == "mother")
            type = FamilyLinkType.Mother;
          else if (famRel.Name.LocalName == "childref")
          {
            if ((string)famRel.Attribute("frel") == "Adopted")
              type = FamilyLinkType.Adopted;
            else
              type = FamilyLinkType.Birth;
          }

          database.Add(new FamilyLink()
          {
            Family = (string)familyElement.Attribute("handle"),
            Individual = (string)famRel.Attribute("hlink"),
            Type = type
          });
        }
        database.Add(family);
      }
    }

    private Citation CreateCitation(XElement citationElement, XElement root, Database db)
    {
      var result = new Citation();
      AddCommonFields(citationElement, result, db);
      if (TryGetDate(citationElement, out var date))
        result.DatePublished = date;

      var sourceHandle = (string)citationElement.Element(grampsNs + "sourceref")?.Attribute("hlink");
      var source = root.Element(grampsNs + "sources")
        .Elements(grampsNs + "source")
        .FirstOrDefault(e => sourceHandle != null && sourceHandle == (string)e.Attribute("handle"));
      if (source != null)
      {
        result.Author = (string)source.Element(grampsNs + "sauthor");
        result.Title = (string)source.Element(grampsNs + "stitle");
        var pubInfo = (string)source.Element(grampsNs + "spubinfo");
        if (!string.IsNullOrEmpty(pubInfo))
        {
          result.Publisher = new Organization()
          {
            Name = pubInfo
          };
          var pubId = result.Publisher.GetPreferredId(db);
          if (db.TryGetValue(pubId, out Organization publisher))
          {
            result.Publisher = publisher;
          }
          else
          {
            result.Publisher.Id.Add(pubId);
            db.Add(result.Publisher);
          }
        }

        var importNote = source.Elements(grampsNs + "noteref")
          .Select(e => _importNotes.TryGetValue((string)e.Attribute("hlink"), out var note) ? note : null)
          .FirstOrDefault(n => n != null)
          ?.Text ?? "";
        var match = Regex.Match(importNote, @": 2 DATE (\d.*)");
        if (match.Success && ExtendedDateRange.TryParse(match.Groups[1].Value, out var pubDate))
          result.DatePublished = pubDate;
      }

      if ((string)citationElement.Element(grampsNs + "srcattribute")?.Attribute("type") == "Url"
        && Uri.TryCreate((string)citationElement.Element(grampsNs + "srcattribute").Attribute("value"), UriKind.Absolute, out var uri)
        && uri.Scheme.StartsWith("http"))
      {
        result.Url = uri;
      }

      var repoRef = (string)citationElement.Element(grampsNs + "reporef")?.Attribute("hlink");
      if (repoRef != null && db.TryGetValue(repoRef, out Organization repo))
        result.Repository = repo;

      result.SetPages((string)citationElement.Element(grampsNs + "page"));

      var urlNote = result.Notes
        .Select(n => new { Note = n, Url = Uri.TryCreate(n.Text, UriKind.Absolute, out var uri) ? uri : null })
        .FirstOrDefault(n => n.Url?.Scheme.StartsWith("http") == true);
      if (urlNote != null
        && (result.Url == null
          || result.Url == urlNote.Url))
      {
        result.Url = urlNote.Url;
        result.Notes.Remove(urlNote.Note);
      }

      return result;
    }

    private Organization CreateRepository(XElement repositoryElement, Database db)
    {
      var result = new Organization();
      AddCommonFields(repositoryElement, result, db);
      result.Name = (string)repositoryElement.Element(grampsNs + "rname");
      return result;
    }

    private Event CreateEvent(XElement eventElement, Database db)
    {
      var eventObj = new Event();
      AddCommonFields(eventElement, eventObj, db);
      
      var eventTypeString = (string)eventElement.Element(grampsNs + "type") ?? "";
      if (Enum.TryParse(eventTypeString.Replace(" ", ""), out EventType eventType))
      {
        eventObj.Type = eventType;
      }
      else
      {
        switch (eventTypeString.ToUpperInvariant())
        {
          case "MARRIAGE BANNS":
            eventObj.Type = EventType.MarriageBann;
            break;
          case "DIVORCE FILING":
            eventObj.Type = EventType.DivorceFiled;
            break;
          case "ADOPTED":
            eventObj.Type = EventType.Adoption;
            break;
          default:
            eventObj.Type = EventType.Generic;
            if (!string.IsNullOrEmpty(eventTypeString))
              eventObj.TypeString = eventTypeString;
            break;
        }
      }
      
      if (TryGetDate(eventElement, out var date))
        eventObj.Date = date;

      var placeHandle = (string)eventElement.Element(grampsNs + "place")?.Attribute("hlink");
      if (!string.IsNullOrEmpty(placeHandle) && db.TryGetValue(placeHandle, out Place place))
        eventObj.Place = place;

      eventObj.Notes.AddRange(eventElement.Elements(grampsNs + "description").Select(e => new Note()
      {
        Text = (string)e
      }));

      return eventObj;
    }

    private bool TryGetDate(XElement element, out ExtendedDateRange dateRange)
    {
      var dateVal = (string)element.Element(grampsNs + "dateval")?.Attribute("val");
      if (!string.IsNullOrEmpty(dateVal))
      {
        var dateType = (string)element.Element(grampsNs + "dateval")?.Attribute("type");
        if (dateType == "before")
          dateRange = new ExtendedDateRange(default, ExtendedDateTime.Parse(dateVal));
        else if (dateType == "after")
          dateRange = new ExtendedDateRange(ExtendedDateTime.Parse(dateVal), default);
        else
        {
          if ((string)element.Element(grampsNs + "dateval")?.Attribute("quality") == "estimated")
            dateRange = new ExtendedDateRange(ExtendedDateTime.Parse(dateVal).WithCertainty(DateCertainty.Estimated));
          else if (dateType == "about")
            dateRange = new ExtendedDateRange(ExtendedDateTime.Parse(dateVal).WithCertainty(DateCertainty.About));
          else
            dateRange = new ExtendedDateRange(ExtendedDateTime.Parse(dateVal));
        }
        return true;
      }
      else if (element.Element(grampsNs + "daterange") != null)
      {
        dateRange = new ExtendedDateRange(
          ExtendedDateTime.Parse((string)element.Element(grampsNs + "daterange").Attribute("start"))
          , ExtendedDateTime.Parse((string)element.Element(grampsNs + "daterange").Attribute("stop"))
          , DateRangeType.Range
        );
        return true;
      }
      else if (element.Element(grampsNs + "datespan") != null)
      {
        dateRange = new ExtendedDateRange(
          ExtendedDateTime.Parse((string)element.Element(grampsNs + "datespan").Attribute("start"))
          , ExtendedDateTime.Parse((string)element.Element(grampsNs + "datespan").Attribute("stop"))
          , DateRangeType.Period
        );
        return true;
      }
      else if (element.Element(grampsNs + "datestr") != null
        && ExtendedDateTime.TryParse((string)element.Element(grampsNs + "datestr").Attribute("val"), out var dateStr))
      {
        dateRange = new ExtendedDateRange(dateStr);
        return true;
      }
      else
      {
        dateRange = default;
        return false;
      }
    }

    private Place CreatePlace(XElement placeInfo, Database db)
    {
      var place = new Place();
      AddCommonFields(placeInfo, place, db);
      place.Names.AddRange(placeInfo.Elements(grampsNs + "pname").Select(e => (string)e.Attribute("value")));
      place.Names.AddRange(placeInfo.Elements(grampsNs + "ptitle").Select(e => (string)e));
      place.Latitude = (double?)placeInfo.Element(grampsNs + "coord")?.Attribute("lat");
      place.Longitude = (double?)placeInfo.Element(grampsNs + "coord")?.Attribute("long");
      return place;
    }

    private void AddCommonFields(XElement element, object primaryObject, Database db)
    {
      if (primaryObject is IHasAttributes attributes)
      {
        foreach (var attr in element.Elements(grampsNs + "attribute"))
        {
          var type = (string)attr.Attribute("type");
          if (type != "Merged Gramps ID")
            attributes.Attributes[type] = (string)attr.Attribute("value");
        }
      }

      if (primaryObject is IHasCitations citations)
      {
        foreach (var citeRef in element.Elements(grampsNs + "citationref"))
        {
          if (db.TryGetValue((string)citeRef.Attribute("hlink"), out Citation citation))
            citations.Citations.Add(citation);
        }
      }
      
      if (primaryObject is IHasId ids)
      {
        ids.Id.Add((string)element.Attribute("id"));
        ids.Id.Add((string)element.Attribute("handle"));
      }

      if (primaryObject is IHasLinks hasLinks)
      {
        foreach (var citeRef in element.Elements(grampsNs + "citationref"))
        {
          if (db.TryGetValue((string)citeRef.Attribute("hlink"), out Link link))
            hasLinks.Links.Add(link);
        }
      }

      if (primaryObject is IHasMedia mediaContainer)
      {
        foreach (var objRef in element.Elements(grampsNs + "objref"))
        {
          if (db.TryGetValue((string)objRef.Attribute("hlink"), out Media media))
          {
            mediaContainer.Media.Add(media);
            foreach (var citeRef in objRef.Elements(grampsNs + "citationref"))
            {
              if (db.TryGetValue((string)citeRef.Attribute("hlink"), out Citation citation))
                media.Citations.Add(citation);
            }
          }
        }
      }

      if (primaryObject is IHasNotes notes)
      {
        foreach (var noteRef in element.Elements(grampsNs + "noteref"))
        {
          if (db.TryGetValue((string)noteRef.Attribute("hlink"), out Note note))
            notes.Notes.Add(note);
        }
      }
    }
  }
}
