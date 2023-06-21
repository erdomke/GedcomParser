﻿using GedcomParser.Model;
using System;
using System.Linq;

namespace GedcomParser
{
  internal class PersonIndexSection : ISection
  {
    private Lookup<Individual, ISection> _personIndex = new Lookup<Individual, ISection>();

    public string Title => "Person Index";

    public string Id => "person-index";

    public void Add(Individual person, ISection section)
    {
      _personIndex.Add(person, section);
    }

    public void Render(HtmlTextWriter html, FencedDivExtension extension)
    {
      html.WriteStartSection(this);

      foreach (var person in _personIndex
        .OrderBy(p => p.Key.Names.First().Surname ?? p.Key.Name.Surname, StringComparer.OrdinalIgnoreCase)
        .ThenBy(p => p.Key.Name.Name, StringComparer.OrdinalIgnoreCase))
      {
        html.WriteStartElement("div");
        html.WriteAttributeString("id", person.Key.Id.Primary);
        html.WriteAttributeString("class", "person-index");
        html.WriteStartElement("div");
        html.WriteAttributeString("class", "person");
        html.WriteElementString("strong", person.Key.Name.Surname);
        html.WriteString(", " + person.Key.Name.Remaining);
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
          html.WriteAttributeString("href", "#" + reference.Id);
          html.WriteString(reference.Title);
          html.WriteEndElement();
        }
        html.WriteEndElement();
        html.WriteEndElement();
      }
      html.WriteEndElement();
      html.WriteEndElement();
    }
  }
}