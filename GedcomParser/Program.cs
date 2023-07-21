using GedcomParser.Model;
using Markdig;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using YamlDotNet.RepresentationModel;

namespace GedcomParser
{
  class Program
  {
    static async Task Main(string[] args)
    {
      //IndexDirectory.ProcessDirectory(@"C:\Users\erdomke\source\repos\FamilyTree\import"
      //  , @"C:\Users\erdomke\source\repos\FamilyTree\target"
      //  , "media");
      //return;

      /*var path = @"C:\Users\erdomke\source\repos\FamilyTree\FamilySearch.yaml";
      var db = new Database()
      {
        BasePath = path
      };
      new FamilySearchJsonLoader().Load(db, Path.Combine(Path.GetDirectoryName(path), "FamilySearch.json"));
      db.RemoveNameOnlyIndividuals();
      db.RemoveUnused();
      db.MakeIdsHumanReadable();
      db.MarkDuplicates();
      //await db.GeocodePlaces();
      new YamlWriter().Write(db, path);
      foreach (var root in new[] { "G97R-YNT", "GKG3-ZSQ", "GSQQ-BFS", "LV44-WQL", "G9PN-WBQ" })
      {
        var renderer = new AncestorRenderer(db, root)
        {
          Graphics = new SixLaborsGraphics()
        };
        var svg = renderer.Render();
        svg.Save($@"C:\Users\erdomke\source\repos\FamilyTree\FamilySearch_{root}.svg");
      }
      return;*/

      RoundTrip(@"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml").Wait();
      GenerateReport(args);
    }

    static void GenerateReport(string[] args)
    {
      var graphics = new SixLaborsGraphics();
      var db = new Database()
      {
        BasePath = @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml"
      };
      var yaml = new YamlStream();
      using (var reader = new StreamReader(db.BasePath))
        yaml.Load(reader);
      var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
      new YamlLoader().Load(db, mapping);

      //var countrySvg = new CountryTimeline(db, ResolvedFamily.Resolve(db.Families(), db)).Render("DomkeEricMatthe19880316");
      //countrySvg.Save(@"C:\Users\erdomke\source\repos\FamilyTree\Countries.svg");
      //return;

      using (var writer = new StreamWriter(@"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.html"))
      {
        var report = new ReportRenderer(db, graphics);
        report.Write(writer);
      }
      
      var renderer = new AncestorRenderer(db, "DomkeEricMatthe19880316")
      {
        Graphics = graphics
      };
      var svg = renderer.Render();
      svg.Save(@"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.svg");
    }

    static async Task RoundTrip(string path)
    {
      var db = new Database();
      var yaml = new YamlStream();
      using (var reader = new StreamReader(path))
        yaml.Load(reader);
      var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
      new YamlLoader().Load(db, mapping);
      //db.MakeIdsHumanReadable();
      db.MoveResidenceEventsToFamily();
      db.CombineConsecutiveResidenceEvents();
      
      var graphics = new SystemDrawingGraphics();
      var baseDir = Path.GetDirectoryName(path);
      var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {
        ".png", ".gif", ".bmp", ".jpg", ".jpeg"
      };
      foreach (var media in db.GetValues<IHasMedia>()
        .SelectMany(m => m.Media)
        .Concat(db.GetValues<Individual>().Select(i => i.Picture).Where(m => m != null))
        .Concat(db.GetValues<IHasEvents>().SelectMany(e => e.Events).SelectMany(e => e.Media))
        .Distinct()
        .Where(m => !string.IsNullOrEmpty(m.Src) 
          && !m.Width.HasValue
          && imageExtensions.Contains(Path.GetExtension(m.Src))))
      {
        try
        {
          using (var stream = File.OpenRead(Path.Combine(baseDir, media.Src)))
          {
            var size = graphics.MeasureImage(stream);
            media.Width = size.Width;
            media.Height = size.Height;
          }
        }
        catch (Exception) { }
      }

      await db.GeocodePlaces();
      new YamlWriter().Write(db, path);
    }

    static void Main_Merge(string[] args)
    {
      var db = new Database();
      var yaml = new YamlStream();
      using (var reader = new StreamReader(@"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml"))
        yaml.Load(reader);
      var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
      var yaml2 = new YamlStream();
      yaml2.Load(new StreamReader(@"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree_Ancestry.gen.yaml"));
      
      new YamlLoader().Load(db, mapping, new[] { (YamlMappingNode)yaml2.Documents[0].RootNode });
      new YamlWriter().Write(db, @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml");
    }

    static void Main_Convert(string[] args)
    {
      var db = new Database();
      new GedcomLoader().Load(db, GStructure.Load(@"C:\Users\erdomke\Downloads\D Family Tree(3).ged"));
      db.MakeIdsHumanReadable();

      var mediaRoot = @"C:\Users\erdomke\Documents\Gramps\Ancestry\";
      using (var doc = JsonDocument.Parse(File.ReadAllText(mediaRoot + "index.json")))
      {
        var records = doc.RootElement.GetProperty("records").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        foreach (var citation in db.Citations().Where(c => c.RecordNumber != null))
        {
          if (records.TryGetValue(citation.RecordNumber.Split(':').Last(), out var path))
            citation.Src = mediaRoot + path;
        }

        var mediaXref = doc.RootElement.GetProperty("media").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        foreach (var media in db.GetValues<IHasMedia>()
          .SelectMany(h => h.Media))
        {
          if (media.Attributes.TryGetValue("RIN", out var rin)
            && mediaXref.TryGetValue(rin, out var path))
            media.Src = mediaRoot + path;
        }
      }
      new YamlWriter().Write(db, @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree_Ancestry.gen.yaml");

      var db2 = new Database();
      new GrampsXmlLoader().Load(db2, XElement.Load(@"C:\Users\erdomke\Downloads\Gramps_2023-05-09.gramps"));
      db2.MakeIdsHumanReadable();
      new YamlWriter().Write(db2, @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml");
    }

    //static void RenderFamilyHtml(string[] args)
    //{
    //  var structure = GStructure.Load(@"C:\Users\erdomke\Downloads\D Family Tree(3).ged");
    //  //var structure = GStructure.Load(@"C:\Users\erdomke\Downloads\Gramps_2022-12-28.ged");
      
    //  var db = new Database();
    //  new GedcomLoader().Load(db, structure);

    //  using (var writer = new StreamWriter(@"C:\Users\erdomke\source\GitHub\GedcomParser\Test3.html"))
    //  using (var html = new HtmlTextWriter(writer))
    //  {
    //    html.WriteStartElement("html");
    //    html.WriteStartElement("body");
    //    html.WriteStartElement("main");
    //    foreach (var family in ResolvedFamily.Resolve(db.Families(), db)
    //      .OrderByDescending(f => f.StartDate))
    //    {
    //      html.WriteStartElement("section");
    //      html.WriteElementString("h2", family.StartDate.ToString("s") + ": " + string.Join(" + ", family.Parents.Select(p => p.Name.Surname)));
          
    //      html.WriteStartElement("ul");
    //      foreach (var parent in family.Parents)
    //      {
    //        html.WriteElementString("li", parent.Name.Name);
    //      }
    //      html.WriteEndElement();
    //      html.WriteStartElement("ul");
    //      foreach (var child in family.Children)
    //      {
    //        html.WriteElementString("li", child.Name.Name);
    //      }
    //      html.WriteEndElement();

    //      html.WriteStartElement("ul");
    //      var familyMembers = family.Parents.Concat(family.Children).ToList();
    //      foreach (var familyEvent in family.Events.OrderBy(e => e.Date.Start))
    //      {
    //        var individual = db.WhereUsed(familyEvent).OfType<Individual>().Intersect(familyMembers).FirstOrDefault();
    //        if (individual == null)
    //          html.WriteElementString("li", $"{familyEvent.Date:s}, {familyEvent.Type}, {familyEvent.Place}");
    //        else
    //          html.WriteElementString("li", $"{familyEvent.Date:s}, {familyEvent.Type} of {individual.Name}, {familyEvent.Place}");
    //      }
    //      html.WriteEndElement();

    //      html.WriteEndElement();
    //    }
    //    html.WriteEndElement();
    //    html.WriteEndElement();
    //    html.WriteEndElement();
    //  }
    //}
  }
}
