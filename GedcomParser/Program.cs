using System;
using System.Linq;

namespace GedcomParser
{
    class Program
    {
        static void Main(string[] args)
        {
            var lines = GStructure.Load(@"C:\Users\erdomke\Downloads\Gramps_2022-12-28.ged");
            Console.WriteLine("Hello World!");
        }
    }
}
