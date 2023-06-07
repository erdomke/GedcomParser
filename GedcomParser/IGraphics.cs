using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser
{
  internal interface IGraphics
  {
    Size MeasureImage(Stream stream);
    Size MeasureText(string font, double pixelHeight, string text);
  }
}
