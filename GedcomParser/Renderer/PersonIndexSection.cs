using GedcomParser.Model;
using GedcomParser.Renderer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GedcomParser
{
  internal class PersonIndexSection : ISection
  {
    private Lookup<Individual, ISection> _personIndex = new Lookup<Individual, ISection>();

    public string Title => "Person Index";

    public string Id => "person-index";

    public ILookup<Individual, ISection> Index => _personIndex;

    public void Add(Individual person, ISection section)
    {
      if (person.Species == Species.Human)
        _personIndex.Add(person, section);
    }

    private static Dictionary<string, Uri> _formats = new Dictionary<string, Uri>()
    {
      { "Ancestry",  new Uri("https://www.ancestry.com/genealogy/records/{ID}") },
      { "Family Search", new Uri("https://www.familysearch.org/tree/person/details/{ID}") },
      { "Find a Grave", new Uri("https://www.findagrave.com/memorial/{ID}") },
      { "LinkedIn", new Uri("https://www.linkedin.com/in/{ID}/") }
    };

    public void Render(HtmlTextWriter html, ReportRenderer renderer, RenderState state)
    {
      var hostNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      html.WriteStartSection(this, state);
      foreach (var person in _personIndex
        .Where(p => !string.IsNullOrEmpty(p.Key.Names.First().Surname ?? p.Key.Name.Surname))
        .OrderBy(p => p.Key.Names.First().Surname ?? p.Key.Name.Surname, StringComparer.OrdinalIgnoreCase)
        .ThenBy(p => p.Key.Name.Name, StringComparer.OrdinalIgnoreCase))
      {
        html.WriteStartElement("div");
        html.WriteAttributeString("id", person.Key.Id.Primary);

        html.WriteStartElement("div");
        html.WriteAttributeString("class", "person-index");

        html.WriteStartElement("div");
        html.WriteAttributeString("class", "person");
        if (!string.IsNullOrEmpty(person.Key.Name.Surname))
        {
          html.WriteElementString("strong", person.Key.Name.Surname);
          html.WriteString((person.Key.Name.SurnameStart == 0 ? " " : ", "));
        }
        html.WriteString(person.Key.Name.Remaining);
        if (person.Key.BirthDate.HasValue || person.Key.DeathDate.HasValue)
        {
          html.WriteStartElement("span");
          html.WriteAttributeString("style", "color:#999;");
          html.WriteString($" ({person.Key.DateString})");
          html.WriteEndElement();
        }
        html.WriteEndElement();
        
        html.WriteStartElement("div");
        html.WriteAttributeString("class", "filler");
        html.WriteEndElement();

        html.WriteStartElement("div");
        html.WriteAttributeString("class", "refs");
        var first = true;
        foreach (var reference in person)
        {
          if (first)
            first = false;
          else
            html.WriteString(", ");
          html.WriteStartElement("a");
          html.WriteAttributeString("class", "onlyPage");
          html.WriteAttributeString("href", "#" + reference.Id);
          //html.WriteString(reference.Title);
          html.WriteEndElement();
        }
        html.WriteEndElement();

        html.WriteEndElement();

        var keyLinks = person.Key.Links
          .Select(l => l.TryGetAbbreviaton(out var link) ? link : null)
          .Where(l => l != null)
          .OrderBy(l => l.Url.Host)
          .ToList();
        if (keyLinks.Count > 0)
        {
          html.WriteStartElement("div");
          html.WriteAttributeString("class", "person-links");
          first = true;
          foreach (var link in keyLinks)
          {
            hostNames.Add(link.Url.Host);
            if (first)
              first = false;
            else
              html.WriteString(", ");
            html.WriteStartElement("a");
            html.WriteAttributeString("href", link.Url.ToString());
            html.WriteString(link.Description);
            html.WriteEndElement();
          }
          html.WriteEndElement();
        }
        html.WriteEndElement();
      }

      html.WriteStartElement("p");
      foreach (var format in _formats.Where(k => hostNames.Contains(k.Value.Host)))
      {
        html.WriteString($"Use the {format.Key} ID to find more information by going to {format.Value}. ");
      }
      html.WriteEndElement();

      html.WriteEndElement();
    }
  }
}
