using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser
{
  internal class SourceListSection : ISection
  {
    public string Title => "Source List";

    public string Id => "source-list";

    public IList<Citation> Citations { get; }

    public SourceListSection(IEnumerable<ResolvedFamily> families)
    {
      Citations = families.SelectMany(f => f.Events)
        .SelectMany(e => e.Event.Citations)
        .Distinct()
        .OrderBy(c =>
        {
          var builder = new StringBuilder();
          c.BuildEqualityString(builder, null);
          return builder.ToString();
        }, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    public void Render(HtmlTextWriter html, FencedDivExtension extension)
    {
      html.WriteStartSection(this);

      html.WriteStartElement("ol");
      html.WriteAttributeString("class", "source-list");
      foreach (var citation in Citations)
      {
        html.WriteStartElement("li");
        html.WriteAttributeString("id", citation.Id.Primary);
        citation.WriteBibliographyEntry(html);
        html.WriteEndElement();
      }
      html.WriteEndElement();

      html.WriteEndElement();
    }
  }
}
