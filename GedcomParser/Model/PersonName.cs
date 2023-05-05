namespace GedcomParser
{
  public struct PersonName
  {
    public int _surnameStart;
    public int _surnameLength;

    public string Name { get; }

    public string Remaining
    {
      get
      {
        if (_surnameLength < 1)
          return Name;

        if (_surnameStart == 0)
        {
          if (_surnameLength == Name.Length)
            return null;
          else
            return Name.Substring(_surnameLength).TrimStart();
        }
        else
        {
          if (_surnameStart + _surnameLength == Name.Length)
            return Name.Substring(0, _surnameStart).TrimEnd();

          var secondStart = _surnameStart + _surnameLength;
          while (secondStart < Name.Length
            && char.IsWhiteSpace(Name[secondStart]))
            secondStart++;

          return Name.Substring(0, _surnameStart) + Name.Substring(secondStart);
        }
      }
    }

    public string Surname => _surnameStart >= 0 && _surnameLength > 0 ? Name.Substring(_surnameStart, _surnameLength) : null;

    public PersonName(string value)
    {
      Name = (value ?? "").Trim();
      _surnameStart = value.IndexOf('/');
      if (_surnameStart >= 0)
      {
        var surnameEnd = value.IndexOf('/', _surnameStart + 1);
        if (surnameEnd > _surnameStart)
        {
          var nextIndex = value.IndexOf('/', surnameEnd + 1);
          if (nextIndex < 0)
          {
            _surnameLength = surnameEnd - _surnameStart - 1;
            Name = value.Substring(0, _surnameStart)
                + value.Substring(_surnameStart + 1, _surnameLength)
                + value.Substring(surnameEnd + 1);
            return;
          }
        }
        
        _surnameStart = -1;
        _surnameLength = 0;
      }
      else
      {
        _surnameLength = 0;
      }
    }

    public string ToMarkup()
    {
      if (_surnameLength == 0)
        return Name;
      var result = Name.Substring(0, _surnameStart) + "/" + Name.Substring(_surnameStart, _surnameLength) + "/";
      if (_surnameStart + _surnameLength < Name.Length)
        result += Name.Substring(_surnameStart + _surnameLength);
      return result;
    }

    public override string ToString()
    {
      return Name.ToString();
    }

    public override int GetHashCode()
    {
      return Name.GetHashCode()
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
