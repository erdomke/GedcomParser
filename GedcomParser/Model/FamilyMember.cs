namespace GedcomParser.Model
{
  internal class FamilyMember
  {
    public Individual Individual { get; }
    public FamilyLinkType Role { get; }

    public FamilyMember(Individual individual, FamilyLinkType role)
    {
      Individual = individual;
      Role = role;
    }
  }
}
