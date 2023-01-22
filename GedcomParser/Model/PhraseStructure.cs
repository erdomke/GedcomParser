namespace GedcomParser
{
    public class PhraseStructure : StructureBase
    {
        public override string Tag => "PHRASE";

        public override string Id => null;

        public string Value { get; }

        public PhraseStructure(string value)
        {
            Value = value;
        }
    }
}
