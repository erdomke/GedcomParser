using System.Collections.Generic;
using System.Text;

namespace GedcomParser.Model
{
  public interface IHasId
  {
    Identifiers Id { get; }

    void BuildEqualityString(StringBuilder builder, Database db);
    string GetPreferredId(Database db);
  }

  public interface IHasAttributes
  {
    Dictionary<string, string> Attributes { get; }
  }

  public interface IHasCitations
  {
    List<Citation> Citations { get; }
  }

  public interface IHasLinks
  {
    List<Link> Links { get; }
  }

  public interface IHasMedia
  {
    List<Media> Media { get; }
  }

  public interface IHasNotes
  {
    List<Note> Notes { get; }
  }

  public interface IPrimaryObject 
    : IHasId
    , IHasAttributes
    , IHasCitations
    , IHasLinks
    , IHasMedia
    , IHasNotes
  {
  }
}
