using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GedcomParser
{
    [DebuggerDisplay("@{Id,nq}@ {Tag,nq}")]
    public abstract class StructureBase
    {
        private List<StructureBase> _children;

        public abstract string Tag { get; }
        public IEnumerable<StructureBase> Children => _children ?? Enumerable.Empty<StructureBase>();
        public abstract string Id { get; }
        public StructureBase Container { get; private set; }

        public virtual void Add(StructureBase structure)
        {
            if (_children == null)
                _children = new List<StructureBase>();
            _children.Add(structure);
        }

        public static StructureBase Load(string path)
        {
            var stack = new Stack<StructureBase>();
            stack.Push(new Structure("_ROOT"));
            using (var reader = new GedcomReader(path))
            {
                while (reader.Read())
                {
                    var st = Factory(stack, reader);
                    var currLevel = stack.Count - 2;
                    while (currLevel >= reader.Level)
                    {
                        stack.Pop();
                        currLevel--;
                    }
                    stack.Peek().Add(st);
                    st.Container = stack.Peek();
                    stack.Push(st);
                }
            }
            while (stack.Count > 1)
                stack.Pop();
            return stack.Pop();
        }

        private static StructureBase Factory(Stack<StructureBase> stack, GedcomReader reader)
        {
            if (reader.Tag == "INDI")
                return new Person(reader.XRef);
            else if (reader.Tag == "NAME" && stack.Peek().Tag == "INDI")
                return new PersonName(reader.Value);
            else if (Event.TryGetEventType(reader.Tag, out var eventType))
                return new Event(eventType);
            else if (reader.Tag == "DATE")
                return new DateStructure(reader.Value);
            else if (reader.Tag == "TIME")
                return new TimeStructure(reader.Value);
            else if (reader.Tag == "PHRASE")
                return new PhraseStructure(reader.Value);
            else
                return new Structure(reader.Tag
                    , reader.XRef
                    , reader.ValueIsPointer ? (object)new Pointer(reader.Value) : reader.Value);
        }
    }
}
