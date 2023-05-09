﻿using GedcomParser.Model;
using SixLabors.Fonts;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace GedcomParser
{
  class Program
  {
    static void Main(string[] args)
    {
      //var db = new Database();
      //new GedcomLoader().Load(db, GStructure.Load(@"C:\Users\erdomke\Downloads\D Family Tree(3).ged"));
      //db.MakeIdsHumanReadable();

      //var mediaRoot = @"C:\Users\erdomke\Documents\Gramps\Ancestry\";
      //using (var doc = JsonDocument.Parse(File.ReadAllText(mediaRoot + "index.json")))
      //{
      //  var records = doc.RootElement.GetProperty("records").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
      //  foreach (var citation in db.Citations().Where(c => c.RecordNumber != null))
      //  {
      //    if (records.TryGetValue(citation.RecordNumber.Split(':').Last(), out var path))
      //      citation.Src = mediaRoot + path;
      //  }

      //  var mediaXref = doc.RootElement.GetProperty("media").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
      //  foreach (var media in db.GetValues<IHasMedia>()
      //    .SelectMany(h => h.Media))
      //  {
      //    if (media.Attributes.TryGetValue("RIN", out var rin)
      //      && mediaXref.TryGetValue(rin, out var path))
      //      media.Src = mediaRoot + path;
      //  }
      //}
      //new YamlWriter().Write(db, @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree2.gen.yaml");

      var db2 = new Database();
      new GrampsXmlLoader().Load(db2, XElement.Load(@"C:\Users\erdomke\Downloads\Gramps_2023-05-08.gramps"));
      db2.MakeIdsHumanReadable();
      new YamlWriter().Write(db2, @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml");
    }

    static void RenderFamilyHtml(string[] args)
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
          var familyMembers = family.Parents.Concat(family.Children).ToList();
          foreach (var familyEvent in family.Events.OrderBy(e => e.Date.Start))
          {
            var individual = db.WhereUsed(familyEvent).OfType<Individual>().Intersect(familyMembers).FirstOrDefault();
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
