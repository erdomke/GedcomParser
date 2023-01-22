using System;

namespace GedcomParser
{
    public class TimeStructure : StructureBase
    {
        public override string Tag => "TIME";

        public override string Id => null;

        public TimeSpan Time { get; }

        public bool IsUtc { get; }

        public TimeStructure(string value)
        {
            IsUtc = value.EndsWith("Z");
            if (IsUtc)
                value = value.TrimEnd('Z');
            Time = TimeSpan.Parse(value);
        }
    }
}
