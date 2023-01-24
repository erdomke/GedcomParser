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

        public IndividualName() { }

        public IndividualName(GStructure structure)
        {
            Name = (PersonName)structure;

            var type = structure.Child("TYPE");
            if (type != null)
            {
                if (Enum.TryParse<NameType>((string)type ?? "Other", true, out var nameType))
                {
                    Type = nameType;
                    _typeString = (string)type.Child("PHRASE");
                }
                else
                {
                    _typeString = (string)type;
                    Type = NameType.Other;
                }
            }
            NamePrefix = (string)structure.Child("NPFX");
            GivenName = (string)structure.Child("GIVN");
            Nickname = (string)structure.Child("NICK");
            SurnamePrefix = (string)structure.Child("SPFX");
            Surname = (string)structure.Child("SURN");
            NameSuffix = (string)structure.Child("NSFX");
            foreach (var tran in structure.Children("TRAN"))
            {
                Translations.Add((string)tran.Child("LANG"), new IndividualName(tran));
            }
        }
    }
}
