using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class GrampsXmlLoader
  {
    private static XNamespace grampsNs = "http://gramps-project.org/xml/1.7.1/";

    private Dictionary<string, Place> _places = new Dictionary<string, Place>();
    private Dictionary<string, Citation> _citations = new Dictionary<string, Citation>();
    private Dictionary<string, Organization> _repositories = new Dictionary<string, Organization>();
    private Dictionary<string, Event> _events = new Dictionary<string, Event>();
    private Dictionary<string, Note> _notes = new Dictionary<string, Note>();

    public void Load(Database database, XElement root)
    {
      foreach (var noteElement in root.Element(grampsNs + "notes").Elements(grampsNs + "note")
        .Where(e => (string)e.Attribute("type") != "GEDCOM import"))
      {
        var note = new Note()
        {
          Text = (string)noteElement.Element(grampsNs + "text")
        };
        note.Id.Add((string)noteElement.Attribute("id"));
        note.Id.Add((string)noteElement.Attribute("handle"));
        _notes[(string)noteElement.Attribute("handle")] = note;
      }

      foreach (var repositoryElement in root.Element(grampsNs + "repositories").Elements(grampsNs + "repository"))
      {
        var repository = CreateRepository(repositoryElement);
        _repositories[(string)repositoryElement.Attribute("handle")] = repository;
        database.Add(repository);
      }

      foreach (var citationElement in root.Element(grampsNs + "citations").Elements(grampsNs + "citation"))
      {
        var citation = CreateCitation(citationElement, root);
        _citations[(string)citationElement.Attribute("handle")] = citation;
        database.Add(citation);
      }

      foreach (var placeElement in root.Element(grampsNs + "places").Elements(grampsNs + "placeobj"))
      {
        var place = CreatePlace(placeElement);
        _places[(string)placeElement.Attribute("handle")] = place;
        database.Add(place);
      }

      foreach (var eventElement in root.Element(grampsNs + "events").Elements(grampsNs + "event"))
      {
        var eventObj = CreateEvent(eventElement);
        _events[(string)eventElement.Attribute("handle")] = eventObj;
        database.Add(eventObj);
      }

      foreach (var individualElement in root.Element(grampsNs + "people").Elements(grampsNs + "person"))
      {
        var individual = new Individual();
        individual.Id.Add((string)individualElement.Attribute("id"));
        individual.Id.Add((string)individualElement.Attribute("handle"));
        if ((string)individualElement.Attribute("gender") == "M")
          individual.Sex = Sex.Male;
        if ((string)individualElement.Attribute("gender") == "F")
          individual.Sex = Sex.Female;
        foreach (var name in individualElement.Elements(grampsNs + "name"))
        {
          var iName = new IndividualName();
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
        foreach (var eventRef in individualElement.Elements(grampsNs + "eventref"))
        {
          if (_events.TryGetValue((string)eventRef.Attribute("hlink"), out var eventObj))
            individual.Events.Add(eventObj);
        }

        AddCitationsNotes(individualElement, individual);

        database.Add(individual);
      }

      foreach (var familyElement in root.Element(grampsNs + "families").Elements(grampsNs + "family"))
      {
        var family = new Family();
        family.Id.Add((string)familyElement.Attribute("id"));
        family.Id.Add((string)familyElement.Attribute("handle"));
        foreach (var eventRef in familyElement.Elements(grampsNs + "eventref"))
        {
          if (_events.TryGetValue((string)eventRef.Attribute("hlink"), out var eventObj))
            family.Events.Add(eventObj);
        }
        AddCitationsNotes(familyElement, family);
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

    private Citation CreateCitation(XElement citationElement, XElement root)
    {
      var result = new Citation();
      result.Id.Add((string)citationElement.Attribute("id"));
      result.Id.Add((string)citationElement.Attribute("handle"));
      var page = (string)citationElement.Element(grampsNs + "page");
      if (!string.IsNullOrEmpty(page))
      {
        if (Uri.TryCreate(page, UriKind.Absolute, out var uri))
          result.Url = uri;
        else
          result.Pages = page;
      }
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
          result.Publisher = new Organization()
          {
            Name = pubInfo
          };
      }

      var repoRef = (string)citationElement.Element(grampsNs + "reporef")?.Attribute("hlink");
      if (repoRef != null && _repositories.TryGetValue(repoRef, out var repo))
        result.Repository = repo;

      foreach (var noteRef in citationElement.Elements(grampsNs + "noteref"))
      {
        if (_notes.TryGetValue((string)noteRef.Attribute("hlink"), out var note))
          result.Notes.Add(note);
      }

      return result;
    }

    private Organization CreateRepository(XElement repositoryElement)
    {
      var result = new Organization();
      result.Id.Add((string)repositoryElement.Attribute("id"));
      result.Name = (string)repositoryElement.Element(grampsNs + "rname");
      return result;
    }

    private Event CreateEvent(XElement eventElement)
    {
      var eventObj = new Event();
      eventObj.Id.Add((string)eventElement.Attribute("id"));
      eventObj.Id.Add((string)eventElement.Attribute("handle"));

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
      if (!string.IsNullOrEmpty(placeHandle) && _places.TryGetValue(placeHandle, out var place))
        eventObj.Place = place;

      eventObj.Notes.AddRange(eventElement.Elements(grampsNs + "description").Select(e => new Note()
      {
        Text = (string)e
      }));

      // Description

      AddCitationsNotes(eventElement, eventObj);

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
          dateRange = new ExtendedDateRange(ExtendedDateTime.Parse(dateVal));
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

    private Place CreatePlace(XElement placeInfo)
    {
      var place = new Place();
      place.Id.Add((string)placeInfo.Attribute("id"));
      place.Id.Add((string)placeInfo.Attribute("handle"));
      place.Names.AddRange(placeInfo.Elements(grampsNs + "pname").Select(e => (string)e.Attribute("value")));
      place.Latitude = (double?)placeInfo.Element(grampsNs + "coord")?.Attribute("lat");
      place.Longitude = (double?)placeInfo.Element(grampsNs + "coord")?.Attribute("long");
      AddCitationsNotes(placeInfo, place);
      return place;
    }

    private void AddCitationsNotes(XElement element, IPrimaryObject primaryObject)
    {
      foreach (var citeRef in element.Elements(grampsNs + "citationref"))
      {
        if (_citations.TryGetValue((string)citeRef.Attribute("hlink"), out var citation))
          primaryObject.Citations.Add(citation);
      }
      foreach (var noteRef in element.Elements(grampsNs + "noteref"))
      {
        if (_notes.TryGetValue((string)noteRef.Attribute("hlink"), out var note))
          primaryObject.Notes.Add(note);
      }
    }
  }
}
