using System.Diagnostics;

namespace GedcomParser
{
    [DebuggerDisplay("{Tag,nq} {SplitName}")]
    public class PersonName : StructureBase
    {
        public override string Tag => "NAME";

        public override string Id => null;

        public string DisplayName => SplitName.Replace("/", "");

        public string SplitName { get; }

        public PersonName(string name)
        {
            SplitName = name;
        }
    }
}
