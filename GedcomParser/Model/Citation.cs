using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GedcomParser.Model
{
  public class Citation : IIndexedObject
  {
    public Identifiers Id { get; } = new Identifiers();

    public string Author { get; set; }
    public string Title { get; set; }
    public string PublicationTitle { get; set; }
    public string Pages { get; set; }
    public Organization Publisher { get; set; }
    public Organization Repository { get; set; }
    public ExtendedDateRange DatePublished { get; set; }
    public ExtendedDateRange DateAccessed { get; set; }
    public string RecordNumber { get; set; }
    public Uri Url { get; set; }
    public string Doi { get; set; }

    public List<Note> Notes { get; } = new List<Note>();
  }
}
