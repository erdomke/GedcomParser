namespace GedcomParser
{
  internal interface ISection
  {
    public string Title { get; }
    public string Id { get; }

    void Render(HtmlTextWriter html, FencedDivExtension extension);
  }
}
