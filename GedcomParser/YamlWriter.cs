using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Core.Tokens;
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
        { "people", new YamlSequenceNode(db.Individuals().Select(Visit)) }
      };
      return new YamlDocument(root);
    }

    private YamlMappingNode Visit(Individual individual)
    {
      var node = new YamlMappingNode
      {
        { "names", new YamlSequenceNode(individual.Names.Select(Visit)) },
        { "sex", new YamlScalarNode(individual.Sex.ToString()) },
        { "events", new YamlSequenceNode(individual.Events.Select(Visit)) },
        { "$ids", new YamlSequenceNode(individual.Id.Select(i => new YamlScalarNode(i))) {
          Style = SequenceStyle.Flow
        } },
      };
      
      return node;
    }

    private YamlMappingNode Visit(IndividualName name)
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

    private YamlMappingNode Visit(Event eventObj)
    {
      var node = new YamlMappingNode
      {
        { "type", new YamlScalarNode(eventObj.Type.ToString()) },
      };

      if (eventObj.Date.HasValue)
        node.Add("date", eventObj.Date.ToString("s"));

      if (eventObj.Place != null)
        node.Add("place", Visit(eventObj.Place));

      node.Add("$ids", new YamlSequenceNode(eventObj.Id.Select(i => new YamlScalarNode(i))) { 
        Style = SequenceStyle.Flow 
      });
      return node;
    }

    private YamlMappingNode Visit(Place place)
    {
      var node = new YamlMappingNode
      {
        { "names", new YamlSequenceNode(place.Names.Select(i => new YamlScalarNode(i))) },
      };

      if (!string.IsNullOrEmpty(place.Type))
        node.Add("type", place.Type);
      if (place.Latitude.HasValue)
        node.Add("lat", place.Latitude.ToString());
      if (place.Longitude.HasValue)
        node.Add("long", place.Longitude.ToString());

      node.Add("$ids", new YamlSequenceNode(place.Id.Select(i => new YamlScalarNode(i)))
      {
        Style = SequenceStyle.Flow
      });
      return node;
    }
  }
}
