using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace GedcomParser
{
  public class YamlWriter
  {
    public void Write(Database db, string path)
    {
      using (var writer = new StreamWriter(path))
        new YamlStream(Write(db)).Save(writer, false);
    }

    public YamlDocument Write(Database db)
    {
      var root = new YamlMappingNode();
      var rootPeople = db.Roots
        .Select(r => db.TryGetValue(r, out Individual individual) ? individual.Id.Primary : null)
        .Where(r => !string.IsNullOrEmpty(r))
        .ToList();
      if (rootPeople.Count > 0)
        root.Add("roots", new YamlSequenceNode(rootPeople.Select(r => new YamlScalarNode(r))));
      if (db.Groups.Count > 0)
      {
        root.Add("groups", new YamlSequenceNode(db.Groups.Select(g =>
        {
          var group = new YamlMappingNode();
          if (!string.IsNullOrEmpty(g.Title))
            group.Add("title", g.Title);
          if (!string.IsNullOrEmpty(g.Description))
            group.Add("description", g.Description);
          var familyIds = g.Ids
            .Select(f => db.TryGetValue(f, out Family family) ? family.Id.Primary : null)
            .Where(f => !string.IsNullOrEmpty(f))
            .ToList();
          if (familyIds.Count > 0)
            group.Add("families", new YamlSequenceNode(familyIds.Select(f => new YamlScalarNode(f))));
          return group;
        })));
      }
      root.Add("people", BuildListingById(db.Individuals(), Visit, "people"));
      root.Add("families", BuildListingById(db.Families(), f => Visit(f, db), "families"));
      root.Add("organizations", BuildListingById(db.Organizations(), Visit, "organizations"));
      root.Add("places", BuildListingById(db.Places(), Visit, "places"));
      root.Add("citations", BuildListingById(db.Citations(), Visit, "citations"));
      return new YamlDocument(root);
    }

    private YamlMappingNode BuildListingById<T>(IEnumerable<T> objects, Func<T, YamlMappingNode> visit, string rootName) where T : IHasId
    {
      var result = new YamlMappingNode();
      foreach (var obj in objects
        .OrderBy(o => GetSortKey(o.DuplicateOf ?? o.Id.Primary), StringComparer.OrdinalIgnoreCase)
        .ThenBy(o => GetSortKey(o.Id.Primary), StringComparer.OrdinalIgnoreCase))
      {
        result.Add(obj.Id.Primary, visit(obj));
      }
      return result;
    }

    private string GetSortKey(string value)
    {
      if (long.TryParse(value, out var lng))
        return lng.ToString("D10");
      return value;
    }

    private YamlMappingNode Visit(Individual individual)
    {
      var node = new YamlMappingNode();
      if (individual.Names.Count == 1)
        node.Add("name", Visit(individual.Names[0]));
      else if (individual.Names.Count > 1)
        node.Add("names", new YamlSequenceNode(individual.Names.Select(Visit)));
      if (individual.Sex != Sex.Unknown)
        node.Add("sex", individual.Sex.ToString());
      if (individual.Species != Species.Human)
        node.Add("species", individual.Species.ToString());
      if (individual.Picture != null)
        node.Add("picture", Media(individual.Picture));
      if (individual.Events.Count > 0)
        node.Add("events", new YamlSequenceNode(individual.Events.Select(Visit)));

      AddCommonProperties(node, individual);

      return node;
    }

    private YamlMappingNode Visit(Family family, Database db)
    {
      var node = new YamlMappingNode();
      var parents = new YamlSequenceNode();
      var allLinks = db.FamilyLinks(family, FamilyLinkType.Other);

      foreach (var link in allLinks.Where(l => l.Type.HasFlag(FamilyLinkType.Parent)))
      {
        if (db.TryGetValue(link.Individual, out Individual individual))
          parents.Add(new YamlMappingNode()
          {
            { "$ref", "#/people/" + individual.Id.Primary }
          });
      }
      if (parents.Any())
        node.Add("parents", parents);

      if (family.Type != FamilyType.Unknown)
        node.Add("type", family.Type.ToString());

      var children = new YamlSequenceNode();
      var childrenList = allLinks
        .Where(l => l.Type.HasFlag(FamilyLinkType.Birth))
        .OrderBy(l => l.Order)
        .Select(l => db.TryGetValue(l.Individual, out Individual individual) ? individual : null)
        .Where(i => i != null)
        .ToList();
      if (childrenList.Count > 0
        && childrenList.All(i => i.BirthDate.HasValue))
        childrenList = childrenList.OrderBy(i => i.BirthDate).ToList();

      foreach (var child in childrenList)
      {
        children.Add(new YamlMappingNode()
        {
          { "$ref", "#/people/" + child.Id.Primary }
        });
      }
      if (children.Any())
        node.Add("children", children);

      var members = new YamlSequenceNode();
      foreach (var link in allLinks.Where(l =>
        !l.Type.HasFlag(FamilyLinkType.Parent)
        && !l.Type.HasFlag(FamilyLinkType.Birth)))
      {
        if (db.TryGetValue(link.Individual, out Individual individual))
          members.Add(new YamlMappingNode()
          {
            { "type", new YamlScalarNode(link.Type.ToString()) },
            { "member", new YamlMappingNode()
            {
              { "$ref", "#/people/" + individual.Id.Primary }
            } }
          });
      }
      if (members.Any())
        node.Add("members", members);
      if (family.Events.Count > 0)
        node.Add("events", new YamlSequenceNode(family.Events.Select(Visit)));
      AddCommonProperties(node, family);
      return node;
    }

    private YamlMappingNode Visit(Citation citation)
    {
      var node = new YamlMappingNode();
      if (!string.IsNullOrEmpty(citation.Author))
        node.Add("author", citation.Author);
      if (!string.IsNullOrEmpty(citation.Title))
        node.Add("title", citation.Title);
      if (citation.DatePublished.HasValue)
        node.Add("date_published", citation.DatePublished.ToString("s"));
      if (!string.IsNullOrEmpty(citation.PublicationTitle))
        node.Add("publication_title", citation.PublicationTitle);
      if (!string.IsNullOrEmpty(citation.Pages))
        node.Add("pages", citation.Pages);
      if (citation.Publisher != null)
      {
        var publisher = new YamlMappingNode()
        {
          Style = MappingStyle.Flow
        };
        publisher.Add("$ref", "#/organizations/" + citation.Publisher.Id.Primary);
        node.Add("publisher", publisher);
      }
      if (citation.Repository != null)
      {
        var respository = new YamlMappingNode()
        {
          Style = MappingStyle.Flow
        };
        respository.Add("$ref", "#/organizations/" + citation.Repository.Id.Primary);
        node.Add("repository", respository);
      }
      if (citation.DateAccessed.HasValue)
        node.Add("date_accessed", citation.DateAccessed.ToString("s"));
      if (!string.IsNullOrEmpty(citation.RecordNumber))
        node.Add("record_number", citation.RecordNumber);
      if (!string.IsNullOrEmpty(citation.Doi))
        node.Add("doi", citation.Doi);
      if (!string.IsNullOrEmpty(citation.Src))
        node.Add("src", citation.Src);
      if (citation.Url != null)
        node.Add("url", citation.Url.ToString());
      AddCommonProperties(node, citation);
      return node;
    }

    private YamlNode Visit(IndividualName name)
    {
      if (name.Translations.Count < 1
        && (string.IsNullOrEmpty(name.Surname) || name.Name.Surname == name.Surname)
        && (string.IsNullOrEmpty(name.GivenName) || name.Name.Remaining == name.GivenName)
        && string.IsNullOrEmpty(name.Nickname))
      {
        if (name.Type == NameType.Birth
          && name.Citations.Count < 1
          && name.Notes.Count < 1)
          return new YamlScalarNode(name.Name.ToMarkup());
        
        var node = new YamlMappingNode();
        node.Add("name", name.Name.ToMarkup());
        if (name.Type != NameType.Other)
          node.Add("type", name.Type.ToString());
        AddCommonProperties(node, name);
        return node;
      }
      else
      {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(name.Name.Name))
          node.Add("name", name.Name.ToMarkup());
        if (name.Type != NameType.Other)
          node.Add("type", name.Type.ToString());
        if (!string.IsNullOrEmpty(name.NamePrefix))
          node.Add("prefix", name.NamePrefix);
        if (!string.IsNullOrEmpty(name.GivenName))
          node.Add("given", name.GivenName);
        if (!string.IsNullOrEmpty(name.Nickname))
          node.Add("nickname", name.Nickname);
        if (!string.IsNullOrEmpty(name.SurnamePrefix))
          node.Add("surname_prefix", name.SurnamePrefix);
        if (!string.IsNullOrEmpty(name.Surname))
          node.Add("surname", name.Surname);
        if (!string.IsNullOrEmpty(name.NameSuffix))
          node.Add("suffix", name.NameSuffix);

        if (name.Translations.Count > 0)
        {
          var translations = new YamlMappingNode();
          foreach (var trans in name.Translations)
            translations.Add(trans.Key, Visit(trans.Value));
          node.Add("langs", translations);
        }

        AddCommonProperties(node, name);

        return node;
      }
    }

    private YamlMappingNode Visit(Event eventObj)
    {
      var node = new YamlMappingNode();

      if (eventObj.Type == EventType.Generic)
      {
        if (!string.IsNullOrEmpty(eventObj.TypeString))
          node.Add("_type", eventObj.TypeString);
      }
      else
      {
        node.Add("type", eventObj.Type.ToString());
      }

      if (eventObj.Date.HasValue)
        node.Add("date", eventObj.Date.ToString("s"));
      
      if (eventObj.Place != null)
      {
        var place = new YamlMappingNode()
        {
          Style = MappingStyle.Flow
        };
        place.Add("$ref", "#/places/" + eventObj.Place.Id.Primary);
        node.Add("place", place);
      }

      if (eventObj.Organization != null)
      {
        node.Add("organization", new YamlMappingNode()
        {
          { "$ref", "#/organizations/" + eventObj.Organization.Id.Primary }
        });
      }

      if (!string.IsNullOrEmpty(eventObj.Description))
        node.Add("description", eventObj.Description);

      AddCommonProperties(node, eventObj);
      
      return node;
    }

    private YamlMappingNode Visit(Organization organization)
    {
      var node = new YamlMappingNode();
      if (!string.IsNullOrEmpty(organization.Name))
        node.Add("name", organization.Name);
      if (organization.Place != null)
        node.Add("place", new YamlMappingNode()
        {
          { "$ref", "#/places/" + organization.Place.Id.Primary }
        });
      AddCommonProperties(node, organization);
      return node;
    }

    private YamlNode Visit(PlaceName placeName)
    {
      if (placeName.Parts.Count < 1 && !placeName.Date.HasValue)
        return new YamlScalarNode(placeName.Name);

      var mapping = new YamlMappingNode();
      if (!string.IsNullOrEmpty(placeName.Name))
        mapping.Add("name", placeName.Name);
      if (placeName.Date.HasValue)
        mapping.Add("date", placeName.Date.ToString("s"));
      foreach (var part in placeName.Parts)
        mapping.Add(part.Key, part.Value);
      AddCommonProperties(mapping, placeName);
      return mapping;
    }

    private YamlMappingNode Visit(Place place)
    {
      var node = new YamlMappingNode();
      if (place.Names.Count == 1)
        node.Add("name", Visit(place.Names[0]));
      else
        node.Add("names", new YamlSequenceNode(place.Names.Select(i => Visit(i))));

      if (place.BoundingBox.Count > 0)
        node.Add("bbox", new YamlSequenceNode(place.BoundingBox.Select(v => new YamlScalarNode(v.ToString())))
        {
          Style = SequenceStyle.Flow
        });
      if (place.Latitude.HasValue)
        node.Add("latitude", place.Latitude.Value.ToString());
      if (place.Longitude.HasValue)
        node.Add("longitude", place.Longitude.Value.ToString());

      AddCommonProperties(node, place);
      return node;
    }

    private void AddCommonProperties(YamlMappingNode mappingNode, object primaryObject)
    {
      if (primaryObject is IHasAttributes hasAttributes)
      {
        foreach (var attr in hasAttributes.Attributes.OrderBy(a => a.Key))
        {
          mappingNode.Add("_" + attr.Key, attr.Value); 
        }
      }

      if (primaryObject is IHasNotes hasNotes)
      {
        var notes = new YamlSequenceNode();
        foreach (var note in hasNotes.Notes)
        {
          if (!string.IsNullOrEmpty(note.MimeType))
          {
            notes.Add(new YamlMappingNode()
            {
              { "text", note.Text },
              { "mimetype", note.MimeType }
            });
          }
          else
          {
            notes.Add(note.Text);
          }
        }
        if (notes.Any())
          mappingNode.Add("notes", notes);
      }

      if (primaryObject is IHasMedia hasMedia)
      {
        var mediaRefs = new YamlSequenceNode();
        foreach (var media in hasMedia.Media
          .OrderBy(m => m.Src ?? m.Description ?? ""))
        {
          mediaRefs.Add(Media(media));
        }
        if (mediaRefs.Any())
          mappingNode.Add("media", mediaRefs);
      }

      if (primaryObject is IHasLinks hasLinks)
      {
        var links = new YamlSequenceNode();
        foreach (var link in hasLinks.Links.OrderBy(l => l.Url?.ToString() ?? ""))
        {
          if (!string.IsNullOrEmpty(link.Description))
          {
            links.Add(new YamlMappingNode()
            {
              { "url", link.Url.ToString() },
              { "description", link.Description }
            });
          }
          else
          {
            links.Add(link.Url.ToString());
          }
        }
        if (links.Any())
          mappingNode.Add("links", links);
      }

      if (primaryObject is IHasCitations hasCitations)
      {
        var citations = new YamlSequenceNode();
        foreach (var citation in hasCitations.Citations.OrderBy(c => c.Id.Primary ?? ""))
        {
          citations.Add(new YamlMappingNode()
          {
            { "$ref", "#/citations/" + citation.Id.Primary }
          });
        }
        if (citations.Any())
          mappingNode.Add("citations", citations);
      }

      if (primaryObject is IHasId hasId
        && !string.IsNullOrEmpty(hasId.DuplicateOf))
      {
        mappingNode.Add("$ref", $"#/places/" + hasId.DuplicateOf);
      }
    }

    public YamlMappingNode Media(Media media)
    {
      var mediaNode = new YamlMappingNode();
      if (!string.IsNullOrEmpty(media.Src))
        mediaNode.Add("src", media.Src);
      if (!string.IsNullOrEmpty(media.Description))
      {
        var scalar = new YamlScalarNode(media.Description);
        if (media.Description.IndexOf('\n') >= 0)
          scalar.Style = YamlDotNet.Core.ScalarStyle.Literal;
        mediaNode.Add("description", scalar);
      }
      if (!string.IsNullOrEmpty(media.Content))
      {
        var scalar = new YamlScalarNode(media.Content);
        if (media.Content.IndexOf('\n') >= 0)
          scalar.Style = YamlDotNet.Core.ScalarStyle.Literal;
        mediaNode.Add("content", scalar);
      }
      if (!string.IsNullOrEmpty(media.MimeType))
        mediaNode.Add("mimetype", media.MimeType);
      if (media.Date.HasValue)
        mediaNode.Add("date", media.Date.ToString("s"));
      if (media.TopicDate.HasValue)
        mediaNode.Add("topic_date", media.TopicDate.ToString("s"));
      if (media.Width.HasValue)
        mediaNode.Add("width", media.Width.Value.ToString());
      if (media.Height.HasValue)
        mediaNode.Add("height", media.Height.ToString());

      if (media.Place != null)
      {
        var place = new YamlMappingNode()
        {
          Style = YamlDotNet.Core.Events.MappingStyle.Flow
        };
        place.Add("$ref", "#/places/" + media.Place.Id.Primary);
        mediaNode.Add("place", place);
      }

      AddCommonProperties(mediaNode, media);
      return mediaNode;
    }
  }
}
