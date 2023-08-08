using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace GedcomParser.Renderer
{
  internal class TitlePageRenderer : ISection
  {
    private Citation _citation;

    public string Title => _citation.Title;

    public string Id => "titlePage";

    public TitlePageRenderer(Citation citation)
    {
      _citation = citation;
    }

    public void Render(HtmlTextWriter html, ReportRenderer renderer, RenderState state)
    {
      html.WriteStartElement("section");
      html.WriteAttributeString("class", "title-page");
      html.WriteStartElement("h1");
      html.WriteAttributeString("class", "title");
      html.WriteAttributeString("id", Id);
      html.WriteString(Title);
      html.WriteEndElement();

      if (!string.IsNullOrEmpty(_citation.Author))
      {
        html.WriteStartElement("p");
        html.WriteAttributeString("class", "author");
        html.WriteString(_citation.Author);
        html.WriteEndElement();
      }

      if (_citation.DatePublished.HasValue)
      {
        html.WriteStartElement("p");
        html.WriteAttributeString("class", "pubDate");
        html.WriteString(_citation.DatePublished.ToString("MMMM yyyy"));
        html.WriteEndElement();
      }

      html.WriteEndElement();
    }
  }
}
