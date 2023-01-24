using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GedcomParser
{
    [DebuggerDisplay("@{Id,nq}@ {Tag,nq}")]
    public class GStructure
    {
        private List<GStructure> _children;
        private string _value;
        private bool _valueIsPointer;

        public GStructure Parent { get; private set; }
        public string Id { get; }
        public string Tag { get; }
        public string Pointer => _valueIsPointer ? _value : null;

        public GStructure(string tag, string id = null, string value = null, bool valueIsPointer = false)
        {
            Tag = tag;
            Id = id;
            _value = value;
            _valueIsPointer = valueIsPointer;
        }

        public virtual GStructure Add(GStructure structure)
        {
            if (_children == null)
                _children = new List<GStructure>();
            _children.Add(structure);
            return this;
        }

        public GStructure Child(string tag)
        {
            return Children().FirstOrDefault(s => s.Tag == tag);
        }

        public IEnumerable<GStructure> Children()
        {
            return _children ?? Enumerable.Empty<GStructure>();
        }

        public IEnumerable<GStructure> Children(string tag)
        {
            return Children().Where(s => s.Tag == tag);
        }

        public IEnumerable<GStructure> Parents()
        {
            var curr = this;
            while (curr.Parent != null)
            {
                yield return curr.Parent;
                curr = curr.Parent;
            }    
        }

        public GStructure Resolve()
        {
            if (_valueIsPointer)
                return Parents().Last().Children().FirstOrDefault(c => c.Id == _value);
            else
                return this;
        }

        public bool TryGetDateRange(out ExtendedDateRange dateRange)
        {
            if (Tag != "DATE" || _valueIsPointer)
            {
                dateRange = default;
                return false;
            }
            else
            {
                var value = _value;
                var time = Child("TIME")?._value;
                if (!string.IsNullOrEmpty(time))
                    value += " " + time;

                return ExtendedDateRange.TryParse(value, out dateRange);
            }
        }

        public static GStructure Parse(string content)
        {
            return Load(new StringReader(content));
        }

        public static GStructure Load(string path)
        {
            return Load(new StreamReader(path));
        }

        public static GStructure Load(TextReader textReader)
        {
            var stack = new Stack<GStructure>();
            stack.Push(new GStructure("_ROOT"));
            using (var reader = new GedcomReader(textReader))
            {
                while (reader.Read())
                {
                    var st = new GStructure(reader.Tag, reader.XRef, reader.Value, reader.ValueIsPointer);
                    var currLevel = stack.Count - 2;
                    while (currLevel >= reader.Level)
                    {
                        stack.Pop();
                        currLevel--;
                    }
                    stack.Peek().Add(st);
                    st.Parent = stack.Peek();
                    stack.Push(st);
                }
            }
            while (stack.Count > 1)
                stack.Pop();
            return stack.Pop();
        }

        public static explicit operator int(GStructure structure)
        {
            return ((int?)structure).Value;
        }

        public static explicit operator int?(GStructure structure)
        {
            if (structure == null 
                || string.IsNullOrEmpty(structure._value)
                || structure._valueIsPointer)
                return default;
            return int.Parse(structure._value);
        }

        public static explicit operator ExtendedDateRange(GStructure structure)
        {
            if (structure == null)
                return default;
            else if (structure.TryGetDateRange(out var range))
                return range;
            else
                throw new FormatException($"A structure with the tag {structure.Tag} cannot represent a date.");
        }

        public static explicit operator ExtendedDateTime(GStructure structure)
        {
            var result = (ExtendedDateRange)structure;
            if (result.Type != DateRangeType.Date)
                throw new FormatException($"The date range {result} does not represent a singular date.");
            return result.Start;
        }

        public static explicit operator DateTime(GStructure structure)
        {
            var result = (ExtendedDateTime)structure;
            if (!result.TryGetDateTime(out var dateTime))
                throw new FormatException($"The date {result} cannot be converted into a DateTime.");
            return dateTime;
        }

        private static bool TryGetPersonName(GStructure structure, out PersonName name)
        {
            name = default;
            if (structure == null)
                return false;
            else if (structure.Tag == "NAME"
                || (structure.Tag == "TRAN" && structure.Parent?.Tag == "NAME"))
                name = new PersonName(structure._value);
            else
                return false;
            return true;
        }

        public static explicit operator PersonName(GStructure structure)
        {
            if (structure == null)
                return default;
            else if (TryGetPersonName(structure, out var name))
                return name;
            else
                throw new FormatException($"A person name cannot occur in a {structure.Tag} structure.");
        }

        public static explicit operator string(GStructure structure)
        {
            if (structure == null)
            {
                return null;
            }
            else if (structure._valueIsPointer)
            {
                return (string)structure.Resolve();
            }
            else if (structure.Children().Any(c => c.Tag == "CONT" || c.Tag == "CONC"))
            {
                var builder = new StringBuilder(structure._value ?? "");
                foreach (var child in structure.Children())
                {
                    if (child.Tag == "CONT")
                        builder.AppendLine();
                    if (!string.IsNullOrEmpty(child._value)
                        && (child.Tag == "CONT" || child.Tag == "CONC"))
                        builder.Append(child._value);
                    else if (!(child.Tag == "CONT" || child.Tag == "CONC"))
                        return builder.ToString();
                }
                return builder.ToString();
            }
            else
            {
                var record = structure.Child("PHRASE") ?? structure;
                if (TryGetPersonName(record, out var name))
                    return name.Name;
                return record._value;
            }
        }
    }
}
