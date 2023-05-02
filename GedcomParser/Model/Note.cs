using System.Collections.Generic;
using System.Diagnostics;

namespace GedcomParser.Model
{
  [DebuggerDisplay("{Text}")]
  public class Note
  {
    public string Text { get; set; }
    public string MimeType { get; set; }
    public string Language { get; set; }
    public Dictionary<string, Note> Translations { get; } = new Dictionary<string, Note>();
  }
}
