using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser.Model
{
  public class Organization : IPrimaryObject
  {
    public Identifiers Id { get; } = new Identifiers();

    public string Name { get; set; }
    public Place Place { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Url { get; set; }

    public List<Citation> Citations { get; } = new List<Citation>();
    public List<Note> Notes { get; } = new List<Note>();
  }
}
