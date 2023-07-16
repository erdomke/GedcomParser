using GedcomParser.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GedcomParser
{
  internal class ReportRenderer
  {
    public IGraphics Graphics { get; }
    public Database Database { get; }
    public IEnumerable<ResolvedFamily> Families { get; }
    public HashSet<string> DirectAncestors { get; }

    public ReportRenderer(Database db, IGraphics graphics)
    {
      Graphics = graphics;
      Database = db;
      Families = ResolvedFamily.Resolve(db.Families(), db);
      DirectAncestors = new HashSet<string>(db.Roots);
      var ancestors = DirectAncestors.ToList();
      for (var i = 0; i < ancestors.Count; i++)
      {
        var parents = db.IndividualLinks(ancestors[i], FamilyLinkType.Birth, FamilyLinkType.Parent);
        foreach (var parent in parents)
        {
          if (DirectAncestors.Add(parent.Individual2))
            ancestors.Add(parent.Individual2);
        }
      }
    }

    public void Write(TextWriter writer)
    {
      var html = new HtmlTextWriter(writer, new HtmlWriterSettings()
      {
        Indent = true,
        IndentChars = "  "
      });
      html.WriteStartElement("html");
      html.WriteStartElement("head");
      html.WriteElementString("style", @"body {
  font-family: Calibri;
  font-size: 10pt;
}
main {
  max-width: 7.5in;
  margin: 0 auto;
}
main p {
  text-align: justify;
}
section {
  margin-top: 1in;
}
time {
  font-weight: bold;
}
figure {
  margin: 0;
  text-align: center;
}
.diagrams {
  display:flex;
  flex-wrap: wrap;
  justify-content:space-between;
  align-items:center;
}
figcaption {
  font-style: italic;
}
.person-index {
  display:flex;
}
.person-index .filler {
  flex: 1;
  border-bottom: 1px dotted black;
}
a {
  color: inherit;
  text-decoration: none;
}
a:hover {
  text-decoration: underline;
}
sup.cite {
  color: #999;
}
.event-descrip {
  flex:1;
}
.gallery {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: flex-start;
  gap: 8px;
  break-inside: avoid;
  margin: 8px 0;
}
.caption-box {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  align-items: flex-start;
  break-inside: avoid;
}

article {
  background: #eee;
}");
      html.WriteEndElement();
      html.WriteStartElement("body");
      html.WriteStartElement("main");
      
      var personIndex = new PersonIndexSection();
      var sourceList = new SourceListSection(Families);
      var sections = FamilyGroupSection
        .Create(Families, Database.Groups, sourceList)
        .OfType<ISection>()
        .ToList();
      foreach (var section in sections.OfType<FamilyGroupSection>())
      {
        foreach (var individual in section.Families
          .SelectMany(f => f.Members.Select(m => m.Individual))
          .Distinct())
        {
          personIndex.Add(individual, section);
        }
      }

      var ancestors = Database.Roots
        .Select(r => new AncestorRenderer(Database, r, 9)
        {
          Graphics = Graphics
        })
        .ToList();
      var defaultIndex = sections.Count();
      foreach (var ancestor in Enumerable.Reverse(ancestors))
      {
        personIndex.Add(ancestor.Individual, ancestor);
        var idx = sections.FindIndex(s => s is FamilyGroupSection family && family.Families.Any(f => f.Members.Any(m => m.Individual == ancestor.Individual)));
        sections.Insert(idx < 0 ? defaultIndex : idx + 1, ancestor);
      }

      sections.Add(personIndex);
      sections.Add(sourceList);

      foreach (var section in sections)
      {
        section.Render(html, this);
      }

      html.WriteEndElement();
      html.WriteEndElement();
      html.WriteEndElement();
    }
  }
}
