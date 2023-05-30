using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser
{
  internal class Rectangle
  {
    private const int nodeHeight = 36;
    private const int nodeWidth = 300;

    public double Left { get; set; }
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public double Top { get; set; }
    public double Width { get; set; } = nodeWidth;
    public double Height { get; set; } = nodeHeight;
    public double MidY => Top + Height / 2;
  }
}
