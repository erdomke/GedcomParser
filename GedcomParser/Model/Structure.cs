using System.Diagnostics;

namespace GedcomParser
{
    [DebuggerDisplay("@{Id,nq}@ {Tag,nq} {Value}")]
    public class Structure : StructureBase
    {
        public override string Tag { get; }

        public override string Id { get; }

        public object Value { get; }

        public Structure(string tag, string id = null, object value = null)
        {
            Tag = tag;
            Id = id;
            Value = value;
        }
    }
}
