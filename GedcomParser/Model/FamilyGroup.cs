using System.Collections.Generic;

namespace GedcomParser.Model
{
  public class FamilyGroup : IHasMedia
  {
    public string Title { get; set; }
    public ExtendedDateTime TopicDate { get; set; }
    public FamilyGroupType Type { get; set; }
    public IEnumerable<string> Ids { get; set; }
    public List<Media> Media { get; } = new List<Media>();
  }
}
