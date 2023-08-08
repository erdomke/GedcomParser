using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace GedcomParser
{
  class Program
  {
    static async Task Main(string[] args)
    {
      //var dbPath = @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml";
      //var db = new Database()
      //  .Load(new YamlLoader(), dbPath);
      //foreach (var mediaGroup in db.Media()
      //  .Where(m => m.Src?.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) == true)
      //  .GroupBy(m => m.Src, StringComparer.OrdinalIgnoreCase))
      //{
      //  var filePath = Path.Combine(Path.GetDirectoryName(dbPath), mediaGroup.Key);
      //  if (File.Exists(filePath))
      //  {
      //    foreach (var media in mediaGroup)
      //    {
      //      media.Content = File.ReadAllText(filePath);
      //      media.Src = null;
      //    }
      //    File.Delete(filePath);
      //  }
      //}
      //db.Write(new YamlWriter(), dbPath);
      //return;

      /*using (var client = new HttpClient())
      {
        var folder = @"C:\Users\erdomke\source\repos\FamilyTree\media";
        var dbPath = @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml";
        var fileNames = new HashSet<string>();
        var db = new Database()
          .Load(new YamlLoader(), dbPath);
        var groups = db.Media()
          .Where(m => m.Src?.StartsWith("http") == true)
          .GroupBy(m => m.Src)
          .ToList();
        var i = 1;
        foreach (var mediaGroup in groups)
        {
          if (Uri.TryCreate(mediaGroup.Key, UriKind.Absolute, out var url))
          {
            Console.WriteLine($"Downloading {i} of {groups.Count}: {url}");
            var pathParts = url.AbsolutePath.Split('/');
            var fileName = "fs_" + pathParts[pathParts.Length - 2] + Path.GetExtension(url.AbsolutePath);
            if (!File.Exists(Path.Combine(folder, fileName)))
            {
              var resp = await client.GetAsync(url);
              if (resp.IsSuccessStatusCode)
              {
                if (!fileNames.Add(fileName))
                  fileName = "fs_" + Guid.NewGuid().ToString("N") + Path.GetExtension(url.AbsolutePath);
                var stream = await resp.Content.ReadAsStreamAsync();
                using (var file = new FileStream(Path.Combine(folder, fileName), FileMode.Create, FileAccess.Write))
                  await stream.CopyToAsync(file);
              }
              else
              {
                continue;
              }
            }

            foreach (var media in mediaGroup)
              media.Src = "media/" + fileName;
          }
          i++;
        }
        db.Write(new YamlWriter(), dbPath);
      }
      return;*/

      /*var source = new Database()
        .Load(new YamlLoader(), @"C:\Users\erdomke\source\repos\FamilyTree\FamilySearch2.yaml");
      var target = new Database()
        .Load(new YamlLoader(), @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml");
      var merge = new DatabaseMerge(source, target);
      merge.Add("PutnamDeaconEdwa16540704", "PutnamEdward16540504");
      merge.Add("PutnamLieutenant16150307", "PutnamThomas16140307");
      merge.Add("HolyokeAnna16210118", "HolyokeAnn16200118");
      merge.Add("PutnamAnn16450825", "PutnamAnn16450825");

      //merge.Add("DavisShirleyJo19320207", "DavisShirleyJo19320207");
      //merge.Add("DavisThomas18210101", "DavisThomasCape18210522");
      //merge.Add("DomkeCarlChrist19191012", "DomkeCarlChrist19191012");
      //merge.Add("RosenbergHelenM19230101", "RosenbergHelenMarga19230127");
      merge.Process();
      merge.Report(@"C:\Users\erdomke\source\repos\FamilyTree\MergeReport2.html");
      //target.MakeIdsHumanReadable();
      target.Write(new YamlWriter(), @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml");
      return;*/

      //IndexDirectory.ProcessDirectory(@"C:\Users\erdomke\source\repos\FamilyTree\import"
      //  , @"C:\Users\erdomke\source\repos\FamilyTree\target"
      //  , "media");
      //return;

      //var path = @"C:\Users\erdomke\source\repos\FamilyTree\FamilySearch2.yaml";
      //var db = new Database()
      //  .Load(new FamilySearchJsonLoader(), Path.Combine(Path.GetDirectoryName(path), "FamilySearch2.json"))
      //  .RemoveNameOnlyIndividuals()
      //  .RemoveUnused()
      //  .MakeIdsHumanReadable();
      //await db.GeocodePlaces();
      //db.MarkDuplicates()
      //  .CombineConsecutiveResidenceEvents()
      //  .MoveResidenceEventsToFamily();
      //db.BasePath = path;
      //db.Write(new YamlWriter(), path);
      ///*foreach (var root in new[] { "G97R-YNT", "GKG3-ZSQ", "GSQQ-BFS", "LV44-WQL", "G9PN-WBQ" })
      //{
      //  var renderer = new AncestorRenderer(db, root)
      //  {
      //    Graphics = new SixLaborsGraphics()
      //  };
      //  var svg = renderer.Render();
      //  svg.Save($@"C:\Users\erdomke\source\repos\FamilyTree\FamilySearch_{root}.svg");
      //}*/
      //return;
      //RoundTrip(@"C:\Users\erdomke\source\repos\FamilyTree\FamilySearch.yaml").Wait();
      //return;

      RoundTrip(@"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml").Wait();
      GenerateReport(args);
    }

    private static void MergeDbs()
    {
      var dbSource = new Database()
        .Load(new FamilySearchJsonLoader(), @"C:\Users\erdomke\source\repos\FamilyTree\FamilySearch.json")
        .RemoveNameOnlyIndividuals()
        .RemoveUnused()
        .MakeIdsHumanReadable()
        .MarkDuplicates();
      dbSource.Write(new YamlWriter(), Path.ChangeExtension(dbSource.BasePath, ".yaml"));

      var destPath = @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml";
      var dbDest = new Database()
        .Load(new YamlLoader(), destPath);
      
      foreach (var hasId in dbSource.GetValues<IHasId>()
        .Where(o => !dbDest.ContainsId(o.Id.Primary)))
        dbDest.Add(hasId);
      foreach (var link in dbSource.FamilyLinks())
        dbDest.Add(link);

      dbDest.Write(new YamlWriter(), destPath);
    }

    static void GenerateReport(string[] args)
    {
      var graphics = new SixLaborsGraphics();

      var db = new Database()
        .Load(new YamlLoader(), @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml");

      var countrySvg = new CountryTimeline(db, ResolvedFamily.Resolve(db.Families(), db)).Render("DomkeEricMatthe19880316");
      countrySvg.Save(@"C:\Users\erdomke\source\repos\FamilyTree\Countries.svg");
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
      var db = new Database()
        .Load(new YamlLoader(), path)
        .CombineConsecutiveResidenceEvents()
        .MoveResidenceEventsToFamily();
      //db.MakeIdsHumanReadable();
      
      
      var graphics = new SystemDrawingGraphics();
      var baseDir = Path.GetDirectoryName(path);
      var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {
        ".png", ".gif", ".bmp", ".jpg", ".jpeg"
      };
      foreach (var media in db.Media()
        .Where(m => !string.IsNullOrEmpty(m.Src) 
          && !m.Width.HasValue
          && imageExtensions.Contains(Path.GetExtension(m.Src)))
        .ToList())
      {
        try
        {
          if (media.Src.StartsWith("http:") || media.Src.StartsWith("https:"))
          {
            // Do nothing
          }
          else
          {
            using (var stream = File.OpenRead(Path.Combine(baseDir, media.Src)))
            {
              var size = graphics.MeasureImage(stream);
              media.Width = size.Width;
              media.Height = size.Height;
            }
          }
        }
        catch (Exception) { }
      }

      //await db.GeocodePlaces();
      db.Write(new YamlWriter(), path);
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
      db.Write(new YamlWriter(), @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml");
    }

    static void Main_Convert(string[] args)
    {
      var db = new Database()
        .Load(new GedcomLoader(), @"C:\Users\erdomke\Downloads\D Family Tree(3).ged")
        .MakeIdsHumanReadable();

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
      db.Write(new YamlWriter(), @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree_Ancestry.gen.yaml");

      var db2 = new Database()
        .Load(new GrampsXmlLoader(), @"C:\Users\erdomke\Downloads\Gramps_2023-05-09.gramps")
        .MakeIdsHumanReadable()
        .Write(new YamlWriter(), @"C:\Users\erdomke\source\repos\FamilyTree\FamilyTree.gen.yaml");
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
