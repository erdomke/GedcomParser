using GedcomParser.Model;
using System.IO;

namespace GedcomParser
{
  public interface IDbWriter
  {
    void Write(Database database, Stream stream);
  }
}
