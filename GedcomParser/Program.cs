using GedcomParser.Model;
using SixLabors.Fonts;

namespace GedcomParser
{
    class Program
    {
        static void Main(string[] args)
        {
            var structure = GStructure.Load(@"C:\Users\erdomke\Downloads\D Family Tree(3).ged");
            var db = new Database();
            new GedcomLoader().Load(db, structure);
            var renderer = new AncestorRenderer()
            {
                Sizer = (fontName, height, text) =>
                {
                    var font = SystemFonts.CreateFont(fontName, (float)height);
                    return TextMeasurer.Measure(text, new TextOptions(font)).Width;
                }
            };
            var svg = renderer.Render(db, "I322438959843");
            svg.Save(@"C:\Users\erdomke\source\GitHub\GedcomParser\Test.svg");
        }
    }
}
