namespace GedcomParser
{
    public struct PersonName
    {
        public int _surnameStart;
        public int _surnameLength;

        public string Name { get; }
        public string Surname => _surnameStart >= 0 ? Name.Substring(_surnameStart, _surnameLength) : null;

        public PersonName(string value)
        {
            Name = value;
            _surnameStart = value == null ? -1 : value.IndexOf('/');
            if (_surnameStart >= 0)
            {
                var surnameEnd = value.IndexOf('/', _surnameStart + 1);
                if (surnameEnd > _surnameStart)
                {
                    _surnameLength = surnameEnd - _surnameStart - 1;
                    Name = value.Substring(0, _surnameStart)
                        + value.Substring(_surnameStart + 1, _surnameLength)
                        + value.Substring(surnameEnd + 1);
                }
                else
                {
                    _surnameStart = -1;
                    _surnameLength = 0;
                }
            }
            else
            {
                _surnameLength = 0;
            }
        }

        public override string ToString()
        {
            return Name?.ToString();
        }

        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0
                ^ _surnameLength
                ^ _surnameStart;
        }

        public override bool Equals(object obj)
        {
            if (obj is PersonName name)
                return Name == name.Name
                    && _surnameStart == name._surnameStart
                    && _surnameLength == name._surnameLength;
            return false;
        }
    }
}
