using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
    [DebuggerDisplay("{Name}")]
    public class Individual
    {
        public string Id { get; set; }
        public PersonName Name => Names.FirstOrDefault()?.Name ?? default;
        public ExtendedDateRange BirthDate => Events.FirstOrDefault(e => e.Type == EventType.Birth)?.Date ?? default;
        public ExtendedDateRange DeathDate => Events.FirstOrDefault(e => e.Type == EventType.Death)?.Date ?? default;
        public List<IndividualName> Names { get; } = new List<IndividualName>();
        public List<Event> Events { get; } = new List<Event>();
        public List<string> FamiliesAsChild { get; } = new List<string>();
        public List<string> FamiliesAsSpouse { get; } = new List<string>();

        public Individual(GStructure structure)
        {
            Id = structure.Id;
            Names.AddRange(structure.Children("NAME").Select(n => new IndividualName(n)));
            Events.AddRange(structure
                .Children()
                .Where(c => Event.TryGetEventType(c.Tag, out var _))
                .Select(c => new Event(c)));
            FamiliesAsChild.AddRange(structure.Children("FAMC").Select(c => c.Pointer));
            FamiliesAsSpouse.AddRange(structure.Children("FAMS").Select(c => c.Pointer));
        }
    }
}
