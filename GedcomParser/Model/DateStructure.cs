namespace GedcomParser
{
    public class DateStructure : StructureBase
    {
        public override string Tag => "DATE";

        public override string Id => null;

        public IDateValue Date { get; }
        
        public override void Add(StructureBase structure)
        {
            if (structure is TimeStructure time)
            {
                (Date as IMutableDateValue)?.SetTime(time.Time, time.IsUtc);
            }
            else if (structure is PhraseStructure phrase)
            {
                (Date as IMutableDateValue)?.SetPhrase(phrase.Value);
            }
            else
            {
                base.Add(structure);
            }
        }

        public DateStructure(string value)
        {
            if (value.StartsWith("FROM "))
            {
                var idx = value.IndexOf(" TO ");
                if (idx > 0)
                    Date = new DatePeriod(DateValue.Parse(value.Substring(5, idx - 5)), DateValue.Parse(value.Substring(idx + 4)));
                else
                    Date = new DatePeriod(DateValue.Parse(value.Substring(5)), null);
            }
            else if (value.StartsWith("TO "))
            {
                Date = new DatePeriod(null, DateValue.Parse(value.Substring(3)));
            }
            else if (value.StartsWith("BET "))
            {
                var idx = value.IndexOf(" AND ");
                if (idx > 0)
                    Date = new DateRange(DateValue.Parse(value.Substring(4, idx - 4)), DateValue.Parse(value.Substring(idx + 5)));
                else
                    Date = DateValue.Parse(value);
            }
            else if (value.StartsWith("AFT "))
            {
                Date = new DateRange(DateValue.Parse(value.Substring(4)), null);
            }
            else if (value.StartsWith("BEF "))
            {
                Date = new DateRange(null, DateValue.Parse(value.Substring(4)));
            }
            else
            {
                Date = DateValue.Parse(value);
            }
        }
    }
}
