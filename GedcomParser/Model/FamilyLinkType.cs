using System;

namespace GedcomParser.Model
{
    [Flags]
    public enum FamilyLinkType
    {
        Other = 0,
        Child = 0x1,
        Birth = 0x2 + Child,
        Adopted = 0x4 + Child,
        Foster = 0x8 + Child,
        Sealing = 0x10 + Child,
        Parent = 0x20,
        Father = 0x40 + Parent,
        Mother = 0x80 + Parent,
        Neighbor = 0x100,
        Godparent = 0x200,
        Friend = 0x400,
        MultipleBirth = 0x800
    }
}
