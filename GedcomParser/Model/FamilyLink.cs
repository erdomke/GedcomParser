using System.Diagnostics;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{Individual} {Type} {Family}")]
  public class FamilyLink
  {
    public string Family { get; set; }
    public string Individual { get; set; }
    public int Order { get; set; }
    public FamilyLinkType Type { get; set; }
  }
}
