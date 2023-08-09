using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser.Renderer
{
  internal class RenderState
  {
    public bool RestartPageNumbers { get; set; }
    public List<string> Scripts { get; } = new List<string>();
  }
}
