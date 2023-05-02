using GedcomParser.Model;
using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace GedcomParser
{
  internal class GrampsXmlLoader
  {
    private static XNamespace grampsNs = "http://gramps-project.org/xml/1.7.1/";

    public void Load(Database database, XElement root)
    {
      foreach (var person in root.Element(grampsNs + "people").Elements(grampsNs + "person"))
      {
        var individual = new Individual();
        individual.Id.Add((string)person.Attribute("id"));
        individual.Id.Add((string)person.Attribute("handle"));
        if ((string)person.Attribute("gender") == "M")
          individual.Sex = Sex.Male;
        if ((string)person.Attribute("gender") == "F")
          individual.Sex = Sex.Female;
        foreach (var name in person.Elements(grampsNs + "name"))
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
        foreach (var eventRef in person.Elements(grampsNs + "eventref"))
        {
          var eventObj = CreateEvent(root, (string)eventRef.Attribute("hlink"));
          if (eventObj != null)
            individual.Events.Add(eventObj);
        }
        database.Add(individual);
      }
    }

    private Event CreateEvent(XElement root, string handle)
    {
      var eventInfo = root.Element(grampsNs + "events")
        .Elements(grampsNs + "event")
        .FirstOrDefault(e => (string)e.Attribute("handle") == handle);
      if (eventInfo == null || string.IsNullOrEmpty(handle))
        return null;

      var eventObj = new Event();
      eventObj.Id.Add((string)eventInfo.Attribute("id"));
      eventObj.Id.Add((string)eventInfo.Attribute("handle"));
      switch (((string)eventInfo.Element(grampsNs + "type")).ToUpperInvariant())
      {
        case "BIRTH":
          eventObj.Type = EventType.Birth;
          break;
        case "DEATH":
          eventObj.Type = EventType.Death;
          break;
        case "BURIAL":
          eventObj.Type = EventType.Burial;
          break;
        case "DEGREE":
          eventObj.Type = EventType.Graduation;
          break;
        case "BAPTISM":
          eventObj.Type = EventType.Baptism;
          break;
        case "RESIDENCE":
          eventObj.Type = EventType.Residence;
          break;
        case "CENSUS":
          eventObj.Type = EventType.Census;
          break;
        case "MARRIAGE":
          eventObj.Type = EventType.Marriage;
          break;
        case "OCCUPATION":
          eventObj.Type = EventType.Occupation;
          break;
        case "ARRIVAL":
          eventObj.Type = EventType.Arrival;
          break;
        case "DEPARTURE":
          eventObj.Type = EventType.Departure;
          break;
        case "NATURALIZATION":
          eventObj.Type = EventType.Naturalization;
          break;
        case "IMMIGRATION":
          eventObj.Type = EventType.Immigration;
          break;
        case "CONFIRMATION":
          eventObj.Type = EventType.Confirmation;
          break;
        default:
          eventObj.Type = EventType.Generic;
          break;
      }
      var dateVal = (string)eventInfo.Element(grampsNs + "dateval")?.Attribute("val");
      if (!string.IsNullOrEmpty(dateVal))
      {
        eventObj.Date = new ExtendedDateRange(ExtendedDateTime.Parse(dateVal));
      }
      else if (eventInfo.Element(grampsNs + "daterange") != null)
      {
        eventObj.Date = new ExtendedDateRange(
          ExtendedDateTime.Parse((string)eventInfo.Element(grampsNs + "daterange").Attribute("start"))
          , ExtendedDateTime.Parse((string)eventInfo.Element(grampsNs + "daterange").Attribute("stop"))
          , DateRangeType.Range
        );
      }
      else if (eventInfo.Element(grampsNs + "datespan") != null)
      {
        eventObj.Date = new ExtendedDateRange(
          ExtendedDateTime.Parse((string)eventInfo.Element(grampsNs + "datespan").Attribute("start"))
          , ExtendedDateTime.Parse((string)eventInfo.Element(grampsNs + "datespan").Attribute("stop"))
          , DateRangeType.Period
        );
      }
      else if (eventInfo.Element(grampsNs + "datestr") != null
        && ExtendedDateTime.TryParse((string)eventInfo.Element(grampsNs + "datestr").Attribute("val"), out var dateStr))
      {
        eventObj.Date = new ExtendedDateRange(dateStr);
      }

      var place = CreatePlace(root, (string)eventInfo.Element(grampsNs + "place")?.Attribute("hlink"));
      if (place != null)
        eventObj.Place = place;

      eventObj.Notes.AddRange(eventInfo.Elements(grampsNs + "description").Select(e => new Note()
      {
        Text = (string)e
      }));

      // Description

      return eventObj;
    }

    private Place CreatePlace(XElement root, string handle)
    {
      var placeInfo = root.Element(grampsNs + "places")
        .Elements(grampsNs + "placeobj")
        .FirstOrDefault(e => (string)e.Attribute("handle") == handle);
      if (placeInfo == null || string.IsNullOrEmpty(handle))
        return null;

      var place = new Place();
      place.Id.Add((string)placeInfo.Attribute("id"));
      place.Id.Add((string)placeInfo.Attribute("handle"));
      place.Names.AddRange(placeInfo.Elements(grampsNs + "pname").Select(e => (string)e.Attribute("value")).Take(1));
      place.Latitude = (double?)placeInfo.Element(grampsNs + "coord")?.Attribute("lat");
      place.Longitude = (double?)placeInfo.Element(grampsNs + "coord")?.Attribute("long");
      place.Type = (string)placeInfo.Attribute("type");
      if (place.Type == "Unknown")
        place.Type = null;
      return place;
    }
  }
}
