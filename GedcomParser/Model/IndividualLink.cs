using System.Diagnostics;

namespace GedcomParser.Model
{
    [DebuggerDisplay("{Individual1} {LinkType1} {LinkType2} {Individual2}")]
    public class IndividualLink
    {
        public string Individual1 { get; set; }
        public FamilyLinkType LinkType1 { get; set; }
        public string Individual2 { get; set; }
        public FamilyLinkType LinkType2 { get; set; }
    }
}
