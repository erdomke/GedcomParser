using GedcomParser.Model;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using System.Linq;
using static System.Collections.Specialized.BitVector32;

namespace GedcomParser
{
  internal class FencedDivRenderer : HtmlObjectRenderer<FencedDiv>
  {
    private readonly FencedDivExtension _extension;

    public FencedDivRenderer(FencedDivExtension extension)
    {
      _extension = extension;
    }

    protected override void Write(HtmlRenderer renderer, FencedDiv obj)
    {
      var html = new HtmlTextWriter(renderer.Writer);
      var personIndex = new PersonIndexSection();
      var sourceList = new SourceListSection(_extension.ResolvedFamilies());
      var sections = FamilyGroupSection
        .Create(_extension.ResolvedFamilies(), obj.Options, sourceList)
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

      var ancestors = obj.Options.AncestorPeople
        .Select(r => new AncestorRenderer(_extension.Database, r, obj.Options.AncestorDepth)
        {
          Graphics = _extension.Graphics
        })
        .ToList();
      var defaultIndex = sections.Count();
      foreach (var ancestor in Enumerable.Reverse(ancestors))
      {
        personIndex.Add(ancestor.Individual, ancestor);
        var idx = sections.FindIndex(s => s is FamilyGroupSection family && family.Families.Any(f => f.Members.Any(m => m.Individual == ancestor.Individual)));
        sections.Insert(idx < 0 ? defaultIndex: idx + 1, ancestor);
      }

      sections.Add(personIndex);
      sections.Add(sourceList);

      foreach (var section in sections)
      {
        section.Render(html, _extension);
      }
    }
  }
}
