using System.Xml.Linq;

namespace GedcomParser.Renderer
{
  internal class MapFigure
  {
    public double Width { get; set; }
    public XElement Map { get; set; }
    public string Caption { get; set; }
  }
}
