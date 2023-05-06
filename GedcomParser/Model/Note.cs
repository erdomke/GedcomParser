using System.Diagnostics;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{Text}")]
  public class Note : IHasId
  {
    public Identifiers Id { get; } = new Identifiers();

    public string Text { get; set; }
    public string MimeType { get; set; }
  }
}
