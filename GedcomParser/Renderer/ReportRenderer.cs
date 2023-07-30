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
    public PersonIndexSection PersonIndex { get; private set; }

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
  page-break-before: always;
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
}

.intro-timeline {
  background: #eee;
  padding: 0.1in;
  display: flex;
  gap:0.1in;
}

.intro-start,
.intro-end {
  font-weight: bold;
  font-size:120%;
}

.intro-fill {
  flex: 1;
  border-top: 2px solid black;
  margin-top: 10px;
  padding: 0 8px;
  display: flex;
}

@media screen {
  .pagedjs_page {
    border: 1px solid #ccc !important;
  }
}

@media print {
  @page {
    size: letter;
    margin: 0.75in 0.5in;
  }
  @page:left {
    @bottom-left {
      content: counter(page);
    }
  }
  @page:right {
    @bottom-right {
      content: counter(page);
    }
  }
}");
      //html.WriteStartElement("script");
      //html.WriteAttributeString("src", "https://unpkg.com/pagedjs/dist/paged.polyfill.js");
      //html.WriteEndElement();
      html.WriteEndElement();
      html.WriteStartElement("body");
      html.WriteStartElement("main");

      PersonIndex = new PersonIndexSection();
      var sourceList = new SourceListSection(Families);
      var sections = DescendentFamilySection
        .Create(Families, Database.Groups, sourceList)
        .OfType<ISection>()
        .ToList();
      foreach (var section in sections.OfType<DescendentFamilySection>())
      {
        foreach (var individual in section.Families
          .SelectMany(f => f.Members
            .Where(m => !m.Role.HasFlag(FamilyLinkType.Child) || DescendantLayout.IncludeChild(section.Families, m.Individual, section.HighlightsOnly ? DirectAncestors : null))
            .Select(m => m.Individual))
          .Distinct())
        {
          PersonIndex.Add(individual, section);
        }
      }
      foreach (var section in sections.OfType<AncestorFamilySection>())
      {
        foreach (var individual in section.Groups
          .SelectMany(g => g.Families)
          .SelectMany(f => f.Members.Select(m => m.Individual))
          .Where(i => DirectAncestors.Intersect(i.Id).Any())
          .Distinct())
        {
          PersonIndex.Add(individual, section);
        }
      }

      var ancestors = Database.Roots
        .Select(r => new AncestorRenderer(Database, r, 6)
        {
          Graphics = Graphics
        })
        .ToList();
      var defaultIndex = sections.Count();
      foreach (var ancestor in Enumerable.Reverse(ancestors))
      {
        PersonIndex.Add(ancestor.Individual, ancestor);
        var idx = sections.FindIndex(s => s is DescendentFamilySection family && family.Families.Any(f => f.Members.Any(m => m.Individual == ancestor.Individual)));
        sections.Insert(idx < 0 ? defaultIndex : idx + 1, ancestor);
      }

      sections.Add(PersonIndex);
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
