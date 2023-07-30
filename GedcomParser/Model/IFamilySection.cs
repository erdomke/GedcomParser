using System.Collections.Generic;

namespace GedcomParser.Model
{
  internal interface IFamilySection : ISection
  {
    ExtendedDateTime StartDate { get; }
    IEnumerable<ResolvedFamily> AllFamilies { get; }
  }
}
