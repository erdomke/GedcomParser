using System;
using System.IO;
using System.Threading.Tasks;

namespace GedcomParser
{
    public class GedcomReader : IDisposable
    {
        private TextReader _reader;

        public int Level { get; private set; }
        public string XRef { get; private set; }
        public string Tag { get; private set; }
        public string Value { get; private set; }
        public bool ValueIsPointer { get; private set; }

        public GedcomReader(string path)
        {
            _reader = new StreamReader(path);
        }

        public GedcomReader(TextReader reader)
        {
            _reader = reader;
        }

        public bool Read()
        {
            var raw = _reader.ReadLine();
            if (raw == null)
                return false;
            Initialize(raw.TrimStart());
            return true;
        }

        public async Task<bool> ReadAsync()
        {
            var raw = await _reader.ReadLineAsync();
            if (raw == null)
                return false;
            Initialize(raw.TrimStart());
            return true;
        }

        private void Initialize(string raw)
        {
            var idx = raw.IndexOf(' ');
            if (idx < 0 || !int.TryParse(raw.Substring(0, idx), out var level))
                throw new InvalidOperationException($"Invalid Gedcom line: {raw}");
            Level = level;

            idx++;
            if (idx >= raw.Length)
                throw new InvalidOperationException($"Invalid Gedcom line: {raw}");

            if (raw[idx] == '@')
            {
                var next = raw.IndexOf('@', idx + 1);
                if (next < 0)
                    throw new InvalidOperationException($"Invalid Gedcom line: {raw}");
                XRef = raw.Substring(idx + 1, next - idx - 1);
                idx = next + 2;
            }
            else
            {
                XRef = null;
            }

            ValueIsPointer = false;
            var valStart = raw.IndexOf(' ', idx);
            if (valStart < 0)
            {
                Tag = raw.Substring(idx);
                Value = null;
            }
            else
            {
                Tag = raw.Substring(idx, valStart - idx);
                Value = raw.Substring(valStart + 1);
                if (Value.Length > 1 && Value[0] == '@')
                {
                    if (Value[1] == '@')
                    {
                        Value = Value.Substring(1).Replace("@@", "@");
                    }
                    else
                    {
                        Value = Value.Substring(1, Value.Length - 2);
                        ValueIsPointer = true;
                    }
                }
                else
                {
                    Value = Value.Replace("@@", "@");
                }
            }
        }

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
        }
    }
}
