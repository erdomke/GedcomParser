using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
      var root = new YamlMappingNode()
      {
        { "people", BuildListingById(db.Individuals(), Visit, "people") },
        { "families", BuildListingById(db.Families(), f => Visit(f, db), "families") },
        { "organizations", BuildListingById(db.Organizations(), Visit, "organizations") },
        { "places", BuildListingById(db.Places(), Visit, "places") },
        { "citations", BuildListingById(db.Citations(), Visit, "citations") }
      };
      return new YamlDocument(root);
    }

    private YamlMappingNode BuildListingById<T>(IEnumerable<T> objects, Func<T, YamlMappingNode> visit, string rootName) where T : IHasId
    {
      var result = new YamlMappingNode();
      foreach (var obj in objects.OrderBy(o => o.Id.Primary, StringComparer.OrdinalIgnoreCase))
      {
        result.Add(obj.Id.Primary, visit(obj));
      }
      return result;
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
      if (individual.Events.Count > 0)
        node.Add("events", new YamlSequenceNode(individual.Events.Select(Visit)));

      AddCommonProperties(node, individual);

      return node;
    }

    private YamlMappingNode Visit(Family family, Database db)
    {
      var node = new YamlMappingNode();
      var parents = new YamlSequenceNode();
      foreach (var link in db.FamilyLinks(family, FamilyLinkType.Parent))
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
      foreach (var link in db.FamilyLinks(family, FamilyLinkType.Child))
      {
        if (db.TryGetValue(link.Individual, out Individual individual))
          children.Add(new YamlMappingNode()
          {
            { "$ref", "#/people/" + individual.Id.Primary }
          });
      }
      if (children.Any())
        node.Add("children", children);
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
        node.Add("publisher", new YamlMappingNode()
        {
          { "$ref", "#/organizations/" + citation.Publisher.Id.Primary }
        });
      }
      if (citation.Repository != null)
      {
        node.Add("repository", new YamlMappingNode()
        {
          { "$ref", "#/organizations/" + citation.Repository.Id.Primary }
        });
      }
      if (citation.DateAccessed.HasValue)
        node.Add("date_accessed", citation.DateAccessed.ToString("s"));
      if (!string.IsNullOrEmpty(citation.RecordNumber))
        node.Add("record_number", citation.RecordNumber);
      if (!string.IsNullOrEmpty(citation.Doi))
        node.Add("doi", citation.Doi);
      if (citation.Url != null)
        node.Add("url", citation.Url.ToString());
      AddCommonProperties(node, citation);
      return node;
    }

    private YamlNode Visit(IndividualName name)
    {
      if (name.Translations.Count < 1
        && (string.IsNullOrEmpty(name.Surname) || name.Name.Surname == name.Surname)
        && (string.IsNullOrEmpty(name.GivenName) || name.Name.Remaining == name.GivenName))
      {
        if (name.Type == NameType.Birth)
          return new YamlScalarNode(name.Name.ToMarkup());
        
        var node = new YamlMappingNode();
        node.Add("name", name.Name.ToMarkup());
        if (name.Type != NameType.Other)
          node.Add("type", name.Type.ToString());
        return node;
      }
      else
      {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(name.Name.Name))
          node.Add("name", name.Name.ToString());
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
          Style = YamlDotNet.Core.Events.MappingStyle.Flow
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

    private YamlMappingNode Visit(Place place)
    {
      var node = new YamlMappingNode();
      if (place.Names.Count == 1)
        node.Add("name", place.Names[0]);
      else
        node.Add("names", new YamlSequenceNode(place.Names.Select(i => new YamlScalarNode(i))));

      if (!string.IsNullOrEmpty(place.Country))
        node.Add("country", place.Country);
      if (!string.IsNullOrEmpty(place.PostalCode))
        node.Add("postal_code", place.PostalCode);
      if (!string.IsNullOrEmpty(place.Locality))
        node.Add("locality", place.Locality);
      if (!string.IsNullOrEmpty(place.StreetAddress))
        node.Add("street_address", place.StreetAddress);
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
        foreach (var attr in hasAttributes.Attributes)
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
        foreach (var media in hasMedia.Media)
        {
          var mediaNode = new YamlMappingNode();
          if (!string.IsNullOrEmpty(media.Src))
            mediaNode.Add("src", media.Src);
          if (!string.IsNullOrEmpty(media.Description))
            mediaNode.Add("description", media.Description);
          if (!string.IsNullOrEmpty(media.MimeType))
            mediaNode.Add("mimetype", media.MimeType);
          if (media.Date.HasValue)
            mediaNode.Add("date", media.Date.ToString("s"));

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
          mediaRefs.Add(mediaNode);
        }
        if (mediaRefs.Any())
          mappingNode.Add("media", mediaRefs);
      }

      if (primaryObject is IHasLinks hasLinks)
      {
        var links = new YamlSequenceNode();
        foreach (var link in hasLinks.Links)
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
        foreach (var citation in hasCitations.Citations)
        {
          citations.Add(new YamlMappingNode()
          {
            { "$ref", "#/citations/" + citation.Id.Primary }
          });
        }
        if (citations.Any())
          mappingNode.Add("citations", citations);
      }
    }
  }
}
