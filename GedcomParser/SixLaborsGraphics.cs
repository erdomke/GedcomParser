using SixLabors.Fonts;
using SixLabors.ImageSharp;
using System.IO;

namespace GedcomParser
{
  internal class SixLaborsGraphics : IGraphics
  {
    public Size MeasureImage(Stream stream)
    {
      using (var image = Image.Load(stream))
        return new Size(image.Width, image.Height);
    }

    public Size MeasureText(string fontName, double pixelHeight, string text)
    {
      var font = SystemFonts.CreateFont(fontName, (float)pixelHeight);
      var rect = TextMeasurer.Measure(text, new TextOptions(font));
      return new Size(rect.Width, rect.Height);
    }
  }
}
