using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace GedcomParser.Renderer
{
  internal class AppendixSection : ISection
  {
    private readonly SourceListSection _sourceList;

    public IEnumerable<Media> Media { get; set; }

    public string Title { get; }

    public string Id { get; }

    public AppendixSection(FamilyGroup familyGroup, SourceListSection sourceList)
    {
      Title = "Appendix: " + familyGroup.Title;
      Id = familyGroup.Title.Replace(' ', '_');
      Media = familyGroup.Media;
      this._sourceList = sourceList;
    }

    public void Render(HtmlTextWriter html, ReportRenderer renderer, RenderState state)
    {
      html.WriteStartSection(this, state);
      GalleryRenderer.Render(html, Media, renderer.Graphics, _sourceList, "article");
      html.WriteEndElement();
    }
  }
}
