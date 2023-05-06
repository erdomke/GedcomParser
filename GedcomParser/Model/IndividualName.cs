using System;
using System.Collections.Generic;

namespace GedcomParser.Model
{
  public class IndividualName : IHasCitations, IHasNotes
  {
    private string _surname;

    public PersonName Name { get; set; }
    public NameType Type { get; set; }
    public string TypeString { get; set; }
    public string NamePrefix { get; set; }
    public string GivenName { get; set; }
    public string Nickname { get; set; }
    public string SurnamePrefix { get; set; }
    public string Surname
    {
      get => _surname ?? Name.Surname;
      set => _surname = value;
    }
    public string NameSuffix { get; set; }
    public Dictionary<string, IndividualName> Translations { get; } = new Dictionary<string, IndividualName>();

    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Note> Notes { get; } = new List<Note>();
  }
}
