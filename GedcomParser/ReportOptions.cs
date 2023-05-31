using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace GedcomParser
{
  public class ReportOptions
  {
    public List<FamilyGroup> Groups { get; } = new List<FamilyGroup>();

    public static ReportOptions Parse(string yamlOptions)
    {
      var result = new ReportOptions();
      var yaml = new YamlStream();
      using (var reader = new StringReader(yamlOptions))
        yaml.Load(reader);
      foreach (var group in yaml.Documents[0].RootNode.Item("groups")
        .EnumerateArray())
      {
        result.Groups.Add(new FamilyGroup()
        {
          Title = (string)group.Item("title"),
          Ids = group.Item("families").EnumerateArray().Select(e => (string)e).ToList()
        });
      }
      return result;
    }
  }

  public class FamilyGroup
  {
    public string Title { get; set; }
    public IEnumerable<string> Ids { get; set; }
  }
}
