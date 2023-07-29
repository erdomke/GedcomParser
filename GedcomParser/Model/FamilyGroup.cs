using System.Collections.Generic;

namespace GedcomParser.Model
{
  public class FamilyGroup
  {
    public string Title { get; set; }
    public string Content { get; set; }
    public ExtendedDateTime TopicDate { get; set; }
    public FamilyGroupType Type { get; set; }
    public IEnumerable<string> Ids { get; set; }
  }
}
