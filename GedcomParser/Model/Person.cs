//using System.Diagnostics;
//using System.Linq;

//namespace GedcomParser
//{
//    [DebuggerDisplay("@{Id,nq}@ {Tag,nq} {Name}")]
//    public class Person : StructureBase
//    {
//        public override string Tag => "INDI";

//        public override string Id { get; }

//        public IDateValue Birth => Children.OfType<Event>().FirstOrDefault(e => e.Type == EventType.Birth)?.Date;

//        public IDateValue Death => Children.OfType<Event>().FirstOrDefault(e => e.Type == EventType.Death)?.Date;

//        public string Name => Children.OfType<PersonName>().FirstOrDefault()?.DisplayName;

//        public Person(string id)
//        {
//            Id = id;
//        }
//    }
//}
