using System;
using System.Drawing;
using System.IO;

namespace GedcomParser
{
  internal class SystemDrawingGraphics : IGraphics
  {
    private Graphics _graphics = Graphics.FromHwnd(IntPtr.Zero);

    public Size MeasureImage(Stream stream)
    {
      var size = Image.FromStream(stream).Size;
      return new Size(size.Width, size.Height);
    }

    public Size MeasureText(string font, double pixelHeight, string text)
    {
      var fontObj = new Font(font, (float)pixelHeight);
      var size = _graphics.MeasureString(text, fontObj);
      return new Size(size.Width, size.Height);
    }
  }
}
