using System.Collections.Generic;

namespace GedcomParser.Model
{
    public class Family : IPrimaryObject
    {
        public Identifiers Id { get; } = new Identifiers();
        public List<Event> Events { get; } = new List<Event>();
    }
}
