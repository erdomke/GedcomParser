namespace GedcomParser.Model
{
  internal class FamilyMember
  {
    public Individual Individual { get; }
    public FamilyLinkType Role { get; }
    public int Order { get; }

    public FamilyMember(Individual individual, FamilyLinkType role, int order)
    {
      Individual = individual;
      Role = role;
      Order = order;  
    }
  }
}
