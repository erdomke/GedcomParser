using System;
using System.Collections.Generic;
using System.Text;

namespace GedcomParser.Model
{
    public interface IPrimaryObject
    {
        Identifiers Id { get; }
    }
}
