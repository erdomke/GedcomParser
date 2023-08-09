using ExCSS;
using GedcomParser.Model;
using GedcomParser.Renderer;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using static Microsoft.Msagl.Layout.Incremental.KDTree;

namespace GedcomParser
{
  internal class DescendentFamilySection : IFamilySection
  {
    private SourceListSection _sourceList;
    
    public string Title { get; set; }
    public List<ResolvedFamily> Families { get; } = new List<ResolvedFamily>();
    public string Id => Families.First().Id.Primary;
    public ExtendedDateTime StartDate { get; set; }
    public bool HighlightsOnly { get; set; }
    IEnumerable<ResolvedFamily> IFamilySection.AllFamilies => Families;
    public IEnumerable<Media> Media { get; set; }

    public DescendentFamilySection(string title, SourceListSection sourceList)
    {
      Title = title;
      _sourceList = sourceList;
    }

    public static IEnumerable<IFamilySection> Create(IEnumerable<ResolvedFamily> resolvedFamilies, IEnumerable<FamilyGroup> groups, SourceListSection sourceList)
    {
      var xref = new Dictionary<string, ResolvedFamily>();
      foreach (var family in resolvedFamilies)
      {
        foreach (var id in family.Id)
          xref.Add(id, family);
      }

      var result = groups
        .Select(g =>
        {
          
          if (g.Type == FamilyGroupType.Ancestors)
          {
            return (IFamilySection)new AncestorFamilySection(g.Title, g.Ids, sourceList)
            {
              StartDate = g.TopicDate,
              Media = g.Media
            };
          }
          else
          {
            var resolved = new DescendentFamilySection(g.Title, sourceList)
            {
              HighlightsOnly = g.Type == FamilyGroupType.DescendantHighlights,
              Media = g.Media
            };
            resolved.Families.AddRange(g.Ids
              .Select(i => xref.TryGetValue(i, out var f) ? f : null)
              .Where(f => f != null)
              .Distinct()
              .OrderBy(f => f.StartDate));
            if (string.IsNullOrEmpty(resolved.Title))
              resolved.Title = string.Join(" + ", resolved.Families.SelectMany(f => f.Parents).Select(p => p.Name.Surname).Distinct());
            resolved.StartDate = g.TopicDate.HasValue ? g.TopicDate : resolved.Families.First().StartDate;
            return resolved;
          }
        })
        .ToList();

      var ancestors = result.OfType<AncestorFamilySection>()
        .SelectMany(a => a.Groups)
        .ToList();
      var assignedFamilies = new HashSet<string>(result
        .OfType<DescendentFamilySection>()
        .SelectMany(s => s.Families)
        .Select(f => f.Id.Primary));
      while (true)
      {
        var incomplete = ancestors.Where(a => a.NextPeople.Count > 0).ToList();
        if (incomplete.Count < 1)
          break;

        foreach (var group in incomplete)
        {
          var newFamilies = group.NextPeople
            .SelectMany(i => resolvedFamilies
              .Where(f => !assignedFamilies.Contains(f.Id.Primary)
                && f.Members.Any(m => m.Individual.Id.Contains(i) && m.Role.HasFlag(FamilyLinkType.Child))))
            .ToList();
          group.Families.AddRange(newFamilies);
          assignedFamilies.UnionWith(newFamilies.Select(f => f.Id.Primary));
          var nextPeople = newFamilies
            .Where(f => f.Children(FamilyLinkType.Birth).Any(i => i.Id.Intersect(group.NextPeople).Any()))
            .SelectMany(f => f.Parents)
            .Select(i => i.Id.Primary)
            .ToList();
          group.NextPeople.Clear();
          group.NextPeople.AddRange(nextPeople);
        }
      }

      foreach (var group in ancestors)
      {
        var ordered = group.Families
          .GroupBy(f => f.Members.Any(i => i.Individual.Id.Primary.Contains(group.RootPersonId)))
          .SelectMany(g => g.Key ? g.OrderBy(f => f.StartDate) : g.OrderByDescending(f => f.StartDate))
          .ToList();
        group.Families.Clear();
        group.Families.AddRange(ordered);
      }
      foreach (var section in result.OfType<AncestorFamilySection>())
      {
        section.Groups.Sort((x, y) => x.Families.First().StartDate.CompareTo(y.Families.First().StartDate) * -1);
        if (!section.StartDate.HasValue)
          section.StartDate = section.Groups.Min(g => g.Families.First().StartDate);
      }

      return result.OrderByDescending(g => g.StartDate).ToList();
    }

    internal static void RenderIntro(IFamilySection section, HtmlTextWriter html, ReportRenderer renderer, SourceListSection sourceList)
    {
      var eventDates = section.AllFamilies
        .SelectMany(f => f.Events)
        .Select(e => e.Event.Date)
        .Where(d => d.HasValue)
        .OrderBy(d => d)
        .ToList();
      var startYear = eventDates[0].Start.Year;
      var endYear = eventDates.Last().End.HasValue ? eventDates.Last().End.Year : eventDates.Last().Start.Year;
      var crossReferencePeople = renderer.PersonIndex.Index
        .Where(g => g.Contains(section) && g.Skip(1).Any())
        .SelectMany(g => CreateLinks(g, section))
        .ToList();
      var leftPeople = crossReferencePeople
        .Where(l => l.Left)
        .GroupBy(l => l.Section)
        .ToList();
      var rightPeople = crossReferencePeople
        .Where(l => !l.Left)
        .GroupBy(l => l.Section)
        .ToList();

      void RenderLinks(IEnumerable<IGrouping<ISection, PersonLink>> sections)
      {
        foreach (var section in sections)
        {
          html.WriteString("See ");
          html.WriteStartElement("a");
          html.WriteAttributeString("href", "#" + section.Key.Id);
          html.WriteAttributeString("class", "withPage");
          html.WriteAttributeString("style", "font-style:italic");
          html.WriteString(section.Key.Title);
          html.WriteEndElement();
          html.WriteString(" for more information about");
          var last = section.Count() - 1;
          var i = 0;
          foreach (var link in section)
          {
            if (i == 0)
              html.WriteString(" ");
            else if (i == last)
              html.WriteString(i > 1 ? ", and " : " and ");
            else
              html.WriteString(", ");
            html.WriteStartElement("a");
            html.WriteAttributeString("href", "#" + link.Individual.Id.Primary);
            html.WriteAttributeString("style", "font-weight:bold;");
            html.WriteString(link.Individual.Name.Name);
            html.WriteEndElement();
            i++;
          }
          html.WriteString(". ");
        }
      }

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "intro-timeline");

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "intro-end");

      if (leftPeople.Count > 0)
        html.WriteString("← ");
      if (endYear > DateTime.Now.Year - 30)
        html.WriteString("Current");
      else
        html.WriteString(Decade(endYear) + "s");
      html.WriteEndElement();

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "intro-fill");

      html.WriteStartElement("div");
      html.WriteAttributeString("style", "max-width:2.75in");
      RenderLinks(leftPeople);
      html.WriteEndElement();

      html.WriteStartElement("div");
      html.WriteAttributeString("style", "flex:1");
      html.WriteEndElement();

      html.WriteStartElement("div");
      html.WriteAttributeString("style", "max-width:2.75in;text-align:right;");
      RenderLinks(rightPeople);
      html.WriteEndElement();

      html.WriteEndElement();

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "intro-start");
      html.WriteString(Decade(startYear) + "s");
      if (rightPeople.Count > 0)
        html.WriteString(" →");
      html.WriteEndElement();

      html.WriteEndElement();

      var slaveHolders = section.AllFamilies
        .SelectMany(f => f.Members.Select(m => m.Individual))
        .Distinct()
        .Where(i => i.Attributes.TryGetValue("Slave Holder", out var value))
        .ToList();
      if (slaveHolders.Count > 0)
      {
        html.WriteStartElement("blockquote");
        html.WriteElementString("strong", "Note: ");
        html.WriteString("The following individuals were noted as owning slaves:");
        html.WriteStartElement("ul");
        foreach (var slaveHolder in slaveHolders)
        {
          html.WriteStartElement("li");
          html.WriteStartElement("a");
          html.WriteAttributeString("href", "#" + slaveHolder.Id.Primary);
          html.WriteString(slaveHolder.Name.Name);
          html.WriteEndElement();
          html.WriteEndElement();
        }
        html.WriteEndElement();
        html.WriteEndElement();
      }

      GalleryRenderer.Render(html, section.Media, renderer.Graphics, sourceList, "article");
    }

    private static int Decade(int year)
    {
      return year - year % 10;
    }

    private record PersonLink(bool Left, Individual Individual, ISection Section);

    private static IEnumerable<PersonLink> CreateLinks(IGrouping<Individual, ISection> entry, IFamilySection current)
    {
      var left = true;
      foreach (var section in entry.OfType<IFamilySection>())
      {
        if (section == current)
          left = false;
        else
          yield return new PersonLink(left, entry.Key, section);
      }
    }

    public void Render(HtmlTextWriter html, ReportRenderer renderer, RenderState state)
    {
      var paraBuilder = new ParagraphBuilder()
      {
        SourceList = _sourceList,
        DirectAncestors = renderer.DirectAncestors
      };
      html.WriteStartSection(this, state);

      foreach (var family in Families.Skip(1))
      {
        html.WriteStartElement("a");
        html.WriteAttributeString("id", family.Id.Primary);
        html.WriteEndElement();
      }

      RenderIntro(this, html, renderer, _sourceList);

      var baseDirectory = Path.GetDirectoryName(renderer.Database.BasePath);

      html.WriteStartElement("div");
      html.WriteAttributeString("class", "diagrams");

      html.WriteStartElement("figure");
      html.WriteAttributeString("class", "decendants");
      var decendantRenderer = new DescendantLayout()
      {
        Graphics = renderer.Graphics
      };
      decendantRenderer.Render(Families, baseDirectory, HighlightsOnly ? renderer.DirectAncestors : null).WriteTo(html);
      html.WriteEndElement();

      var mapRenderer = new MapRenderer();
      if (mapRenderer.TryRender(Families, baseDirectory, out var figures))
      {
        foreach (var figure in figures)
        {
          html.WriteStartElement("figure");
          html.WriteAttributeString("class", "map");
          figure.Map.WriteTo(html);
          if (!string.IsNullOrEmpty(figure.Caption))
          {
            html.WriteStartElement("figcaption");
            html.WriteAttributeString("style", $"max-width:{Math.Max(2.5*96, figure.Width)}px;");
            html.WriteString(figure.Caption);
            html.WriteEndElement();
          }
          html.WriteEndElement();
        }
      }

      var timelineRenderer = new TimelineRenderer()
      {
        Graphics = renderer.Graphics
      };
      var timelineSvg = timelineRenderer.Render(Families, baseDirectory, HighlightsOnly ? renderer.DirectAncestors : null);
      if (timelineSvg != null)
      {
        html.WriteStartElement("figure");
        html.WriteAttributeString("class", "timeline");
        timelineSvg.WriteTo(html);
        html.WriteEndElement();
        html.WriteEndElement();
      }

      foreach (var family in Families)
      {
        var allEvents = ResolvedEventGroup.Group(family.Events);

        paraBuilder.StartParagraph(html);
        foreach (var ev in allEvents)
          paraBuilder.WriteEvent(html, ev, true);
        paraBuilder.EndParagraph(html);

        GalleryRenderer.Render(html, family.Media
          .Concat(family.Events.SelectMany(e => e.Event.Media.Concat(e.Related.SelectMany(r => r.Media)))), renderer.Graphics, _sourceList);
      }
      html.WriteEndElement();
    }
    
  }
}
