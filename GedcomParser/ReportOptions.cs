using GedcomParser.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace GedcomParser
{
  public class ReportOptions
  {
    public int AncestorDepth { get; set; }

    public List<string> AncestorPeople { get; } = new List<string>();

    public List<FamilyGroup> Groups { get; } = new List<FamilyGroup>();

    public static ReportOptions Parse(string yamlOptions)
    {
      var result = new ReportOptions();
      var yaml = new YamlStream();
      using (var reader = new StringReader(yamlOptions))
        yaml.Load(reader);

      foreach (var property in yaml.Documents[0].RootNode.EnumerateObject())
      {
        switch (((string)property.Key).ToLowerInvariant())
        {
          case "ancestor-chart":
            result.AncestorDepth = int.Parse((string)property.Value.Item("depth"));
            result.AncestorPeople.AddRange(property.Value.Item("people").EnumerateArray().Select(n => (string)n));
            break;
          case "groups":
            foreach (var group in property.Value.EnumerateArray())
            {
              result.Groups.Add(new FamilyGroup()
              {
                Title = (string)group.Item("title"),
                Ids = group.Item("families").EnumerateArray().Select(e => (string)e).ToList()
              });
            }
            break;
        }
      }
      return result;
    }
  }
}
