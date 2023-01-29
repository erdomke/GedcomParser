using System.Diagnostics;

namespace GedcomParser.Model
{
    [DebuggerDisplay("{SourceId} {Page}")]
    public class Citation : IPrimaryObject
    {
        public Identifiers Id { get; } = new Identifiers();
        public string SourceId { get; set; }
        public string Page { get; set; }
        public ExtendedDateRange Date { get; set; }
        public ConfidenceLevel Confidence { get; set; }
        public string RecordId { get; set; }
        public string Note { get; set; }
    }
}
