using System.Collections.Generic;

namespace GedcomParser.Model
{
  public class PlaceName
  {
    public string Name { get; set; }
    public ExtendedDateRange Date { get; set; }
    public List<KeyValuePair<string, string>> Parts { get; } = new List<KeyValuePair<string, string>>();
  }
}
