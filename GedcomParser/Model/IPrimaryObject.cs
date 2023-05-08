using System.Collections.Generic;

namespace GedcomParser.Model
{
  public interface IHasId
  {
    Identifiers Id { get; }

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
