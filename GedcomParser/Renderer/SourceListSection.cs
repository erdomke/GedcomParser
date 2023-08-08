using GedcomParser.Model;
using GedcomParser.Renderer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GedcomParser
{
  internal class SourceListSection : ISection
  {
    private List<Citation> _citations = new List<Citation>();

    public string Title => "Source List";

    public string Id => "source-list";

    public int Add(Citation citation)
    {
      var idx = _citations.FindIndex(c => c.Id.Primary == citation.Id.Primary);
      if (idx >= 0)
        return idx + 1;
      _citations.Add(citation);
      return _citations.Count;
    }

    public void Render(HtmlTextWriter html, ReportRenderer renderer, RenderState state)
    {
      html.WriteStartSection(this, state);

      html.WriteStartElement("ol");
      html.WriteAttributeString("class", "source-list");
      foreach (var citation in _citations)
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
