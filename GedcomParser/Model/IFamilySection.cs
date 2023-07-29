namespace GedcomParser.Model
{
  internal interface IFamilySection : ISection
  {
    ExtendedDateTime StartDate { get; }
  }
}
