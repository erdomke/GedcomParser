﻿using GedcomParser.Model;
using SixLabors.Fonts;
using System.IO;
using System.Linq;

namespace GedcomParser
{
  class Program
  {
    static void Main(string[] args)
    {
      var structure = GStructure.Load(@"C:\Users\erdomke\Downloads\D Family Tree(3).ged");
      //var structure = GStructure.Load(@"C:\Users\erdomke\Downloads\Gramps_2022-12-28.ged");
      
      var db = new Database();
      new GedcomLoader().Load(db, structure);

      using (var writer = new StreamWriter(@"C:\Users\erdomke\source\GitHub\GedcomParser\Test3.html"))
      using (var html = new HtmlTextWriter(writer))
      {
        html.WriteStartElement("html");
        html.WriteStartElement("body");
        html.WriteStartElement("main");
        foreach (var family in ResolvedFamily.Resolve(db.Families(), db)
          .OrderByDescending(f => f.StartDate))
        {
          html.WriteStartElement("section");
          html.WriteElementString("h2", family.StartDate.ToString("s") + ": " + string.Join(" + ", family.Parents.Select(p => p.Name.Surname)));
          
          html.WriteStartElement("ul");
          foreach (var parent in family.Parents)
          {
            html.WriteElementString("li", parent.Name.Name);
          }
          html.WriteEndElement();
          html.WriteStartElement("ul");
          foreach (var child in family.Children)
          {
            html.WriteElementString("li", child.Name.Name);
          }
          html.WriteEndElement();

          html.WriteStartElement("ul");
          foreach (var familyEvent in family.Events.OrderBy(e => e.Date.Start))
          {
            var individual = family.Parents.Concat(family.Children).FirstOrDefault(i => i.Id.Contains(familyEvent.Context));
            if (individual == null)
              html.WriteElementString("li", $"{familyEvent.Date:s}, {familyEvent.Type}, {familyEvent.Place}");
            else
              html.WriteElementString("li", $"{familyEvent.Date:s}, {familyEvent.Type} of {individual.Name}, {familyEvent.Place}");
          }
          html.WriteEndElement();

          html.WriteEndElement();
        }
        html.WriteEndElement();
        html.WriteEndElement();
        html.WriteEndElement();
      }
    }

    static void RenderSvg(string[] args)
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
      svg.Save(@"C:\Users\erdomke\source\GitHub\GedcomParser\Test3.svg");
    }
  }
}
