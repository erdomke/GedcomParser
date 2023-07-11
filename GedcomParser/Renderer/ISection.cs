using GedcomParser.Renderer;

namespace GedcomParser
{
  internal interface ISection
  {
    public string Title { get; }
    public string Id { get; }

    void Render(HtmlTextWriter html, ReportRenderer renderer);
  }
}
