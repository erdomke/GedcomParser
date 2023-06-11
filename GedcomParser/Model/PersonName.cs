namespace GedcomParser
{
  public struct PersonName
  {
    public string Name { get; }

    public string Remaining
    {
      get
      {
        if (SurnameLength < 1)
          return Name;

        if (SurnameStart == 0)
        {
          if (SurnameLength == Name.Length)
            return null;
          else
            return Name.Substring(SurnameLength).TrimStart();
        }
        else
        {
          if (SurnameStart + SurnameLength == Name.Length)
            return Name.Substring(0, SurnameStart).TrimEnd();

          var secondStart = SurnameStart + SurnameLength;
          while (secondStart < Name.Length
            && char.IsWhiteSpace(Name[secondStart]))
            secondStart++;

          return Name.Substring(0, SurnameStart) + Name.Substring(secondStart);
        }
      }
    }

    public string Surname => SurnameStart >= 0 && SurnameLength > 0 ? Name.Substring(SurnameStart, SurnameLength) : null;
    public int SurnameStart { get; }
    public int SurnameLength { get; }

    public PersonName(string value)
    {
      Name = (value ?? "").Trim();
      SurnameStart = value.IndexOf('/');
      if (SurnameStart >= 0)
      {
        var surnameEnd = value.IndexOf('/', SurnameStart + 1);
        if (surnameEnd > SurnameStart)
        {
          var nextIndex = value.IndexOf('/', surnameEnd + 1);
          if (nextIndex < 0)
          {
            SurnameLength = surnameEnd - SurnameStart - 1;
            Name = value.Substring(0, SurnameStart)
                + value.Substring(SurnameStart + 1, SurnameLength)
                + value.Substring(surnameEnd + 1);
            return;
          }
        }
        
        SurnameStart = -1;
        SurnameLength = 0;
      }
      else
      {
        SurnameLength = 0;
      }
    }

    public string ToMarkup()
    {
      if (SurnameLength == 0)
        return Name;
      var result = Name.Substring(0, SurnameStart) + "/" + Name.Substring(SurnameStart, SurnameLength) + "/";
      if (SurnameStart + SurnameLength < Name.Length)
        result += Name.Substring(SurnameStart + SurnameLength);
      return result;
    }

    public override string ToString()
    {
      return Name.ToString();
    }

    public override int GetHashCode()
    {
      return Name.GetHashCode()
          ^ SurnameLength
          ^ SurnameStart;
    }

    public override bool Equals(object obj)
    {
      if (obj is PersonName name)
        return Name == name.Name
            && SurnameStart == name.SurnameStart
            && SurnameLength == name.SurnameLength;
      return false;
    }
  }
}
