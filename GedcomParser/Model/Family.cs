using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
    public class Family
    {
        public string Id { get; set; }
        public List<string> Parents { get; } = new List<string>();
        public List<string> Children { get; } = new List<string>();

        public Family() { }

        public Family(GStructure structure)
        {
            Id = structure.Id;
            Parents.AddRange(structure.Children().Where(c => c.Tag == "HUSB" || c.Tag == "WIFE").Select(c => c.Pointer));
            Children.AddRange(structure.Children("CHIL").Select(c => c.Pointer));
        }
    }
}
