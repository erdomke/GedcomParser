using System;
using System.Collections.Generic;
using System.Text;

namespace GedcomParser
{
    class Pointer
    {
        public string Target { get; }

        public Pointer(string target)
        {
            Target = target;
        }
    }
}
