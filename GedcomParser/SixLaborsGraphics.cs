using SixLabors.Fonts;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace GedcomParser
{
  internal class SixLaborsGraphics : IGraphics
  {
    private Dictionary<string, Font> _fontCache = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);

    public Size MeasureImage(Stream stream)
    {
      using (var image = Image.Load(stream))
        return new Size(image.Width, image.Height);
    }

    public Size MeasureText(string fontName, double pixelHeight, string text)
    {
      var key = fontName + pixelHeight.ToString("0.#");
      if (!_fontCache.TryGetValue(key, out var font))
      {
        font = SystemFonts.CreateFont(fontName, (float)pixelHeight);
        _fontCache[key] = font;
      }
      var rect = TextMeasurer.Measure(text, new TextOptions(font));
      return new Size(rect.Width, rect.Height);
    }
  }
}
