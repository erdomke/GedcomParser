using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace GedcomParser
{
  internal class YamlLoader
  {
    private Dictionary<string, YamlMappingNode> toProcess = new Dictionary<string, YamlMappingNode>();

    public void Load(Database database, YamlMappingNode root, IEnumerable<YamlMappingNode> alternates = null)
    {
      var keysToProcess = new HashSet<string>();
      foreach (var doc in (alternates ?? Enumerable.Empty<YamlMappingNode>())
        .Reverse()
        .Concat(new[] { root }))
      {
        foreach (var group in doc.Children)
        {
          if (group.Value is YamlMappingNode idGroup)
          {
            foreach (var id in idGroup)
            {
              var key = $"#/{(string)group.Key}/{(string)id.Key}";
              toProcess[key] = (YamlMappingNode)id.Value;
              if (doc == root)
                keysToProcess.Add(key);
            }
          }
        }
      }

      foreach (var kvp in toProcess
        .Where(k => keysToProcess.Contains(k.Key))
        .OrderBy(k =>
        {
          if (k.Key.StartsWith("#/citations/"))
            return 0;
          else if (k.Key.StartsWith("#/places/"))
            return 1;
          else if (k.Key.StartsWith("#/organizations/"))
            return 2;
          else if (k.Key.StartsWith("#/people/"))
            return 3;
          else if (k.Key.StartsWith("#/families/"))
            return 4;
          return 5;
        }))
      {
        if (kvp.Key.StartsWith("#/citations/"))
          Create(kvp.Key, kvp.Value, database, Citation);
        else if (kvp.Key.StartsWith("#/places/"))
          Create(kvp.Key, kvp.Value, database, Place);
        else if (kvp.Key.StartsWith("#/organizations/"))
          Create(kvp.Key, kvp.Value, database, Organization);
        else if (kvp.Key.StartsWith("#/people/"))
          Create(kvp.Key, kvp.Value, database, Individual);
        else if (kvp.Key.StartsWith("#/families/"))
          Create(kvp.Key, kvp.Value, database, Family);
      }
    }

    private Individual Individual(string id, YamlMappingNode node, Database database)
    {
      var individual = new Individual();
      if (!string.IsNullOrEmpty(id))
        individual.Id.Add(id);
      foreach (var property in node.Children)
      {
        switch ((string)property.Key)
        {
          case "name":
            individual.Names.Add(IndividualName(property.Value, database));
            break;
          case "names":
            if (!(property.Value is YamlSequenceNode nameSequence))
              throw new InvalidOperationException($"The `names` of {id} must be a sequence.");
            individual.Names.AddRange(nameSequence.Children.Select(c => IndividualName(c, database)));
            break;
          case "sex":
            individual.Sex = Enum.Parse<Sex>((string)property.Value);
            break;
          case "events":
            individual.Events.AddRange(((YamlSequenceNode)property.Value).Children
              .OfType<YamlMappingNode>()
              .Select(c => Event(c, database)));
            break;
        }
      }
      return individual;
    }

    private Family Family(string id, YamlMappingNode node, Database database)
    {
      var family = new Family();
      if (!string.IsNullOrEmpty(id))
        family.Id.Add(id);
      foreach (var property in node.Children)
      {
        switch ((string)property.Key)
        {
          case "parents":
            foreach (var parent in ((YamlSequenceNode)property.Value).Children
              .OfType<YamlMappingNode>()
              .Select(c => Create(null, c, database, Individual)))
            {
              database.Add(new FamilyLink()
              {
                Family = family.Id.Primary,
                Individual = parent.Id.Primary,
                Type = parent.Sex == Sex.Male ? FamilyLinkType.Father
                  : (parent.Sex == Sex.Female ? FamilyLinkType.Mother : FamilyLinkType.Parent)
              });
            }
            break;
          case "children":
            foreach (var child in ((YamlSequenceNode)property.Value).Children
              .OfType<YamlMappingNode>()
              .Select(c => Create(null, c, database, Individual)))
            {
              database.Add(new FamilyLink()
              {
                Family = family.Id.Primary,
                Individual = child.Id.Primary,
                Type = FamilyLinkType.Birth
              });
            }
            break;
          case "type":
            family.Type = Enum.Parse<FamilyType>((string)property.Value);
            break;
          case "events":
            family.Events.AddRange(((YamlSequenceNode)property.Value).Children
              .OfType<YamlMappingNode>()
              .Select(c => Event(c, database)));
            break;
        }
      }
      return family;
    }

    private Event Event(YamlMappingNode node, Database database)
    {
      var eventObj = new Event();
      foreach (var property in node.Children)
      {
        switch ((string)property.Key)
        {
          case "type":
            eventObj.Type = Enum.Parse<EventType>((string)property.Value);
            break;
          case "_type":
            eventObj.TypeString = (string)property.Value;
            break;
          case "date":
            eventObj.Date = ExtendedDateRange.Parse((string)property.Value);
            break;
          case "place":
            eventObj.Place = Create(null, property.Value as YamlMappingNode, database, Place);
            break;
          case "organization":
            eventObj.Organization = Create(null, property.Value as YamlMappingNode, database, Organization);
            break;
        }
      }
      AddCommonProperties(eventObj, node, database);
      return eventObj;
    }

    private IndividualName IndividualName(YamlNode node, Database database)
    {
      if (node is YamlMappingNode nameNode)
      {
        var name = new IndividualName();
        foreach (var property in nameNode.Children)
        {
          switch ((string)property.Key)
          {
            case "name":
              name.Name = new PersonName((string)property.Value);
              break;
            case "type":
              name.Type = Enum.Parse<NameType>((string)property.Value);
              break;
            case "prefix":
              name.NamePrefix = (string)property.Value;
              break;
            case "given":
              name.GivenName = (string)property.Value;
              break;
            case "nickname":
              name.Nickname = (string)property.Value;
              break;
            case "surname_prefix":
              name.SurnamePrefix = (string)property.Value;
              break;
            case "surname":
              name.Surname = (string)property.Value;
              break;
            case "suffix":
              name.NameSuffix = (string)property.Value;
              break;
            case "langs":
              foreach (var trans in ((YamlMappingNode)property.Value).Children)
                name.Translations.Add((string)trans.Key, IndividualName(trans.Value, database));
              break;
          }
        }
        AddCommonProperties(name, nameNode, database);
        return name;
      }
      else
      {
        return new IndividualName()
        {
          Name = new PersonName((string)node),
          Type = NameType.Birth
        };
      }
    }


    private Citation Citation(string id, YamlMappingNode node, Database database)
    {
      var citation = new Citation();
      if (!string.IsNullOrEmpty(id))
        citation.Id.Add(id);
      foreach (var property in node.Children)
      {
        switch ((string)property.Key)
        {
          case "author":
            citation.Author = (string)property.Value;
            break;
          case "title":
            citation.Title = (string)property.Value;
            break;
          case "date_published":
            citation.DatePublished = ExtendedDateRange.Parse((string)property.Value);
            break;
          case "publication_title":
            citation.PublicationTitle = (string)property.Value;
            break;
          case "pages":
            citation.SetPages((string)property.Value);
            break;
          case "publisher":
            citation.Publisher = Create(null, property.Value as YamlMappingNode, database, Organization);
            break;
          case "repository":
            citation.Repository = Create(null, property.Value as YamlMappingNode, database, Organization);
            break;
          case "date_accessed":
            citation.DateAccessed = ExtendedDateRange.Parse((string)property.Value);
            break;
          case "record_number":
            citation.RecordNumber = (string)property.Value;
            break;
          case "doi":
            citation.Doi = (string)property.Value;
            break;
          case "src":
            citation.Src = (string)property.Value;
            break;
          case "url":
            citation.Url = new Uri((string)property.Value);
            break;
        }
      }
      return citation;
    }

    private Organization Organization(string id, YamlMappingNode node, Database database)
    {
      var organization = new Organization();
      if (!string.IsNullOrEmpty(id))
        organization.Id.Add(id);
      foreach (var property in node.Children)
      {
        switch ((string)property.Key)
        {
          case "name":
            organization.Name = (string)property.Value;
            break;
          case "place":
            organization.Place = Create(null, property.Value as YamlMappingNode, database, Place);
            break;
        }
      }
      return organization;
    }

    private PlaceName PlaceName(YamlNode node, Database database)
    {
      if (node is YamlMappingNode nameNode)
      {
        var name = new PlaceName();
        foreach (var property in nameNode.Children)
        {
          var key = (string)property.Key;
          switch (key)
          {
            case "name":
              name.Name = (string)property.Value;
              break;
            case "date":
              name.Date = ExtendedDateRange.Parse((string)property.Value);
              break;
            case "links":
            case "notes":
            case "citations":
            case "media":
              // Do nothing
              break;
            default:
              if (!key.StartsWith("_") && property.Value is YamlScalarNode)
                name.Parts.Add(new KeyValuePair<string, string>(key, (string)property.Value));
              break;
          }
        }
        AddCommonProperties(name, nameNode, database);
        return name;
      }
      else
      {
        return new PlaceName()
        {
          Name = (string)node
        };
      }
    }

    private Place Place(string id, YamlMappingNode node, Database database)
    {
      var place = new Place();
      if (!string.IsNullOrEmpty(id))
        place.Id.Add(id);
      foreach (var property in node.Children)
      {
        switch ((string)property.Key)
        {
          case "name":
            place.Names.Add(PlaceName(property.Value, database));
            break;
          case "names":
            place.Names.AddRange(((YamlSequenceNode)property.Value).Children.Select(c => PlaceName(c, database)));
            break;
          case "bbox":
            place.BoundingBox.AddRange(((YamlSequenceNode)property.Value).Children.Select(c => double.Parse((string)c)));
            break;
          case "latitude":
            place.Latitude = double.Parse((string)property.Value);
            break;
          case "longitude":
            place.Longitude = double.Parse((string)property.Value);
            break;
        }
      }
      return place;
    }

    private T Create<T>(string refId, YamlMappingNode node, Database database, Func<string, YamlMappingNode, Database, T> factory) where T : IHasId
    {
      var id = string.IsNullOrEmpty(refId) ? null : refId.Split('/').Last();
      if (!string.IsNullOrEmpty(id)
        && database.TryGetValue(id, out T existing))
        return existing;

      if (node == null)
      {
        if (string.IsNullOrEmpty(refId))
          throw new InvalidOperationException("Cannot create an object without an id.");
        if (!toProcess.TryGetValue(refId, out node))
          throw new InvalidOperationException($"Can't find the node with the id {refId}");
      }

      if (node.Children.TryGetValue("$ref", out var refNode))
      {
        if ((string)refNode == refId)
          throw new InvalidOperationException($"Infinite loop exception: {refId}.");
        return Create((string)refNode, null, database, factory);
      }
      else
      {
        var result = factory(id, node, database);
        database.Add(result);
        AddCommonProperties(result, node, database);
        return result;
      }
    }

    private void AddCommonProperties(object result, YamlMappingNode node, Database database)
    {
      if (result is IHasAttributes hasAttributes)
      {
        foreach (var property in node.Children
          .Where(k => ((string)k.Key).StartsWith("_")))
        {
          if (result is Event && (string)property.Key == "_type")
            ; // Do nothing
          else
            hasAttributes.Attributes[((string)property.Key).TrimStart('_')] = (string)property.Value;
        }
      }

      if (result is IHasNotes hasNotes
        && node.Children.TryGetValue("notes", out var notesNode)
        && notesNode is YamlSequenceNode notes)
      {
        foreach (var noteNode in notes.Children)
        {
          if (noteNode is YamlMappingNode mapping)
          {
            var note = new Note();
            foreach (var property in mapping.Children)
            {
              switch ((string)property.Key)
              {
                case "text":
                  note.Text = (string)property.Value;
                  break;
                case "mimetype":
                  note.MimeType = (string)property.Value;
                  break;
                default:
                  throw new NotSupportedException();
              }
            }
            hasNotes.Notes.Add(note);
          }
          else
          {
            hasNotes.Notes.Add(new Note()
            {
              Text = (string)noteNode
            });
          }
        }
      }

      if (result is IHasMedia hasMedia
        && node.Children.TryGetValue("media", out var mediaList)
        && mediaList is YamlSequenceNode mediaListNode)
      {
        foreach (var mediaNode in mediaListNode.Children.OfType<YamlMappingNode>())
        {
          var media = new Media();
          foreach (var property in mediaNode.Children)
          {
            switch ((string)property.Key)
            {
              case "src":
                media.Src = (string)property.Value;
                break;
              case "description":
                media.Description = (string)property.Value;
                break;
              case "mimetype":
                media.MimeType = (string)property.Value;
                break;
              case "date":
                media.Date = ExtendedDateRange.Parse((string)property.Value);
                break;
              case "place":
                media.Place = Create(null, property.Value as YamlMappingNode, database, Place);
                break;
            }
          }
          AddCommonProperties(media, mediaNode, database);
          hasMedia.Media.Add(media);
        }
      }

      if (result is IHasLinks hasLinks
        && node.Children.TryGetValue("links", out var linksNode)
        && linksNode is YamlSequenceNode links)
      {
        foreach (var linkNode in links.Children)
        {
          if (linkNode is YamlMappingNode mapping)
          {
            var link = new Link();
            foreach (var property in mapping.Children)
            {
              switch ((string)property.Key)
              {
                case "url":
                  link.Url = new Uri((string)property.Value);
                  break;
                case "description":
                  link.Description = (string)property.Value;
                  break;
                default:
                  throw new NotSupportedException();
              }
            }
            hasLinks.Links.Add(link);
          }
          else
          {
            hasLinks.Links.Add(new Link()
            {
              Url = new Uri((string)linkNode)
            });
          }
        }
      }

      if (result is IHasCitations hasCitations
        && node.Children.TryGetValue("citations", out var citationsNode)
        && citationsNode is YamlSequenceNode citations)
      {
        foreach (var citationNode in citations.Children.OfType<YamlMappingNode>())
        {
          hasCitations.Citations.Add(Create(null, citationNode, database, Citation));
        }
      }
    }
  }
}
