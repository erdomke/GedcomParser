using GedcomParser.Model;
using System.IO;

namespace GedcomParser
{
  public interface IDbLoader
  {
    void Load(Database database, Stream stream);
  }
}
