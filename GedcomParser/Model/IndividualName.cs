using System;
using System.Collections.Generic;

namespace GedcomParser.Model
{
    public class IndividualName
    {
        private string _typeString;
        private string _surname;

        public PersonName Name { get; set; }
        public NameType Type { get; set; }
        public string TypeString
        {
            get => _typeString ?? Type.ToString();
            set => _typeString = value;
        }
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
    }
}
