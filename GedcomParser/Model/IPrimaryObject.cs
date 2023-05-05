using System.Collections.Generic;

namespace GedcomParser.Model
{
  public interface IPrimaryObject : IIndexedObject
  {
    List<Citation> Citations { get; }
    List<Note> Notes { get; }
  }
}
