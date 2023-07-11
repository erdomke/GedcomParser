using Markdig.Renderers;
using Markdig.Renderers.Html;
using System;
using System.Linq;

namespace GedcomParser
{
  internal class FencedDivRenderer : HtmlObjectRenderer<FencedDiv>
  {
    private readonly FencedDivExtension _extension;

    public FencedDivRenderer(FencedDivExtension extension)
    {
      _extension = extension;
    }

    protected override void Write(HtmlRenderer renderer, FencedDiv obj)
    {
      throw new NotSupportedException();
    }
  }
}
