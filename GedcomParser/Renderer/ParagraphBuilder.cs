﻿using GedcomParser.Model;
using Markdig.Renderers;
using Markdig;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GedcomParser.Renderer
{
  internal class ParagraphBuilder
  {
    private HashSet<string> _existingReferences = new HashSet<string>();
    private bool _previousSubjectIsFamily = false;
    private bool _inParagraph = false;

    public IEnumerable<Individual> PreviousSubject { get; set; }
    public bool IncludeBurialInformation { get; set; } = true;
    public bool IncludeAges { get; set; } = true;
    public SourceListSection SourceList { get; set; }
    public HashSet<string> DirectAncestors { get; set; }
    public string MonthStyle { get; set; } = "MMMM";

    public void StartParagraph(HtmlTextWriter html)
    {
      html.WriteStartElement("p");
      PreviousSubject = null;
      _previousSubjectIsFamily = false;
      _existingReferences.Clear();
      _inParagraph = false;
    }

    public void EndParagraph(HtmlTextWriter html)
    {
      html.WriteEndElement();
    }

    public void WriteEvent(HtmlTextWriter html, ResolvedEventGroup eventGroup, bool includeDate)
    {
      if (_inParagraph)
        html.WriteString(" ");
      _inParagraph = true;
      if (eventGroup.Events.Any(e => (e.Event.Type == EventType.Birth || e.Event.Type == EventType.Adoption) && e.Secondary.Count > 0))
      {
        var births = eventGroup.Events
          .Where(e => (e.Event.Type == EventType.Birth || e.Event.Type == EventType.Adoption))
          .ToList();
        var deaths = eventGroup.Events
          .Where(e => e.Event.Type == EventType.Death)
          .ToDictionary(e => e.Primary.First().Id.Primary);

        AddNames(html, births.First().Secondary, NameForm.AutoName, births.Count > 1 ? default : births.First().Event.Date);
        SetSubject(births.First().Secondary, true);
        if (births.Any(e => e.Event.Type == EventType.Adoption))
          html.WriteString(" adopted " + (eventGroup.Exact ? "" : "at least "));
        else
          html.WriteString(" gave birth to " + (eventGroup.Exact ? "" : "at least "));
        
        html.WriteString(ChildWord(births.SelectMany(e => e.Primary)));
        html.WriteString(" — ");
        var first = true;
        foreach (var birth in births)
        {
          if (first)
            first = false;
          else
            html.WriteString("; ");
          if (birth.Primary.First().Sex == Sex.Male)
            html.WriteString("a boy ");
          else if (birth.Primary.First().Sex == Sex.Female)
            html.WriteString("a girl ");
          AddNames(html, birth.Primary, NameForm.All, default);
          AddDate(html, birth.Event.Date, true);
          if (births.Count < 3 || DirectAncestors.Intersect(birth.Primary.SelectMany(i => i.Id)).Any())
            AddPlace(html, birth.Event);
          if (deaths.TryGetValue(birth.Primary.First().Id.Primary, out var deathEvent))
          {
            if (deathEvent.Event.Date.HasValue)
              html.WriteString(" (died" + RenderDateRange(deathEvent.Event.Date, null, MonthStyle) + ")");
            else
              html.WriteString(" (deceased)");
          }
        }
        html.WriteString(".");
      }
      else if (eventGroup.Events.Any(e => e.Event.Type == EventType.Occupation))
      {
        AddNames(html, eventGroup.Events[0].Primary, NameForm.AutoPronounUpper, default);
        SetSubject(eventGroup.Events[0].Primary, false);
        html.WriteString(" worked");
        AddPlace(html, eventGroup.Events[0].Event);
        var first = true;
        foreach (var ev in eventGroup.Events)
        {
          if (first)
            first = false;
          else
            html.WriteString(", ");
          if (ev.Event.Attributes.TryGetValue("Occupation", out var occupation))
          {
            var word = "a";
            if (occupation.TrimStart().ToUpperInvariant().IndexOfAny(new[] { 'A', 'E', 'I', 'O', 'U' }) == 0)
              word = "an";
            html.WriteString($" as {word} ");
            html.WriteString(occupation);
          }
          AddDate(html, ev.Event.Date, includeDate);
        }
        html.WriteString(".");
      }
      else if (eventGroup.Events.Any(e => e.Event.Type == EventType.Degree
        || e.Event.Type == EventType.Graduation))
      {
        AddNames(html, eventGroup.Events[0].Primary, NameForm.AutoPronounUpper, default);
        SetSubject(eventGroup.Events[0].Primary, false);
        html.WriteString(" graduated");
        var first = true;
        foreach (var ev in eventGroup.Events)
        {
          if (first)
            first = false;
          else
            html.WriteString(", ");

          if (ev.Event.Attributes.TryGetValue("Degree", out var degree))
          {
            var word = "a";
            if (degree.TrimStart().ToUpperInvariant().IndexOfAny(new[] { 'A', 'E', 'I', 'O', 'U' }) == 0)
              word = "an";
            html.WriteString($" with {word} ");
            html.WriteString(degree);
          }
          AddPlace(html, ev.Event, "from");
          AddDate(html, ev.Event.Date, includeDate, ev.Primary[0]);
        }
        html.WriteString(".");
      }
      else
      {
        var first = true;
        foreach (var ev in eventGroup.Events)
        {
          if (first)
            first = false;
          else
            html.WriteString(" ");
          WriteEvent(html, ev, includeDate);
        }
      }

      var eventCitations = eventGroup.Events.SelectMany(e => e.Event.Citations)
        .Select(c => new { Id = c.Id.Primary, Index = SourceList.Add(c) })
        .Where(c => c.Index >= 0)
        .GroupBy(c => c.Index)
        .Select(g => g.First())
        .OrderBy(c => c.Index)
        .ToList();
      if (eventCitations.Count > 0)
      {
        html.WriteString(" ");
        html.WriteStartElement("small");
        html.WriteAttributeString("class", "cite");
        html.WriteString("[");
        var first = true;
        foreach (var citation in eventCitations)
        {
          if (first)
            first = false;
          else
            html.WriteString(", ");
          html.WriteStartElement("a");
          html.WriteAttributeString("href", "#" + citation.Id);
          html.WriteString((citation.Index + 1).ToString());
          html.WriteEndElement();
        }
        html.WriteString("]");
        html.WriteEndElement();
      }
    }

    private string ChildWord(IEnumerable<Individual> individuals)
    {
      var count = individuals.Count();
      if (individuals.Any(i => i.Species == Species.Human))
        return count == 1 ? "1 child" : count + " children";
      else if (individuals.Select(i => i.Species).Distinct().Skip(1).Any())
        return count == 1 ? "1 pet" : count + " pets";
      else
      {
        var animalName = individuals.First().Species.ToString().Replace('_', ' ').ToLowerInvariant();
        return count == 1 ? "1 " + animalName : count + " " + animalName + "s";
      }
    }

    public void WriteEvent(HtmlTextWriter html, ResolvedEvent ev, bool includeDate)
    {
      if (ev.Event.Type == EventType.Birth)
      {
        AddNames(html, ev.Primary, NameForm.All, default);
        SetSubject(ev.Primary, false);
        html.WriteString(" was born");
        AddDate(html, ev.Event.Date, includeDate);
        if (ev.Secondary.Count > 0)
        {
          html.WriteString(" to ");
          AddNames(html, ev.Secondary, NameForm.AutoName, ev.Event.Date);
        }
        AddPlace(html, ev.Event);
      }
      else if (ev.Event.Type == EventType.Death)
      {
        AddNames(html, ev.Primary, NameForm.AutoName, default);
        SetSubject(ev.Primary, false);
        html.WriteString(" died");
        AddDate(html, ev.Event.Date, includeDate, ev.Primary.First());
        if (ev.Event.Attributes.TryGetValue("Cause", out var cause))
        {
          html.WriteString(" of ");
          html.WriteString(cause);
        }
        AddPlace(html, ev.Event, "in");
      }
      else if (ev.Event.Type == EventType.Degree)
      {
        AddNames(html, ev.Primary, NameForm.AutoPronounUpper, ev.Event.Date);
        SetSubject(ev.Primary, false);
        html.WriteString(" graduated");
        if (ev.Event.Attributes.TryGetValue("Degree", out var degree))
        {
          html.WriteString(" with a ");
          html.WriteString(degree);
        }
        AddPlace(html, ev.Event, "from");
        AddDate(html, ev.Event.Date, includeDate);
      }
      else if (ev.Event.Type == EventType.MilitaryService)
      {
        AddNames(html, ev.Primary, NameForm.AutoPronounUpper, ev.Event.Date);
        SetSubject(ev.Primary, false);
        html.WriteString(" served in the ");
        if (ev.Event.Attributes.TryGetValue("Military", out var military))
          html.WriteString(military);
        else
          html.WriteString("military");
        AddPlace(html, ev.Event, "from");
        AddDate(html, ev.Event.Date, includeDate);
      }
      else if (ev.Event.Type == EventType.Occupation)
      {
        AddNames(html, ev.Primary, NameForm.AutoPronounUpper, default);
        SetSubject(ev.Primary, false);
        html.WriteString(" worked");
        if (ev.Event.Attributes.TryGetValue("Occupation", out var occupation))
        {
          html.WriteString(" as a ");
          html.WriteString(occupation);
        }
        AddDate(html, ev.Event.Date, includeDate, ev.Primary.First());
        AddPlace(html, ev.Event);
      }
      else if (ev.Event.Type == EventType.Marriage)
      {
        AddNames(html, ev.Primary, NameForm.AutoName | NameForm.Maiden, ev.Event.Date);
        SetSubject(ev.Primary, true);
        html.WriteString(" were married");
        AddDate(html, ev.Event.Date, includeDate);
        AddPlace(html, ev.Event);
      }
      else if (string.Equals(ev.Event.TypeString, "Diagnosis", StringComparison.OrdinalIgnoreCase))
      {
        AddNames(html, ev.Primary, NameForm.AutoPronounUpper, ev.Event.Date);
        SetSubject(ev.Primary, false);
        html.WriteString(" was diagnosed");
        if (ev.Event.Attributes.TryGetValue("Diagnosis", out var diagnosis))
        {
          html.WriteString(" with ");
          html.WriteString(diagnosis);
        }
        AddDate(html, ev.Event.Date, includeDate);
      }
      else
      {
        var nameType = ev.Event.Type == EventType.Engagement ? NameForm.AutoName : NameForm.AutoPronounUpper;
        AddNames(html, ev.Primary, nameType, default);
        SetSubject(ev.Primary, ev.Primary.Skip(1).Any());
        var placeFirst = false;
        var placeWord = "at";
        switch (ev.Event.Type)
        {
          case EventType.Baptism:
            html.WriteString(" was baptized");
            placeFirst = true;
            break;
          case EventType.Christening:
            html.WriteString(" was christened");
            break;
          case EventType.Confirmation:
            html.WriteString(" underwent confirmation");
            placeFirst = true;
            break;
          case EventType.Immigration:
            html.WriteString(" immigrated");
            break;
          case EventType.Residence:
            html.WriteString(" resided");
            placeWord = "in";
            placeFirst = true;
            break;
          case EventType.Engagement:
            html.WriteString(" got engaged");
            placeFirst = true;
            break;
          default:
            if (string.Equals(ev.Event.TypeString, "Met", StringComparison.OrdinalIgnoreCase))
            {
              html.WriteString(" first met");
            }
            else
            {
              html.WriteString(" underwent " + ev.Event.Type.ToString().ToLowerInvariant());
            }
            break;
        }
        if (placeFirst)
          AddPlace(html, ev.Event, placeWord);
        AddDate(html, ev.Event.Date, includeDate);
        if (!placeFirst)
          AddPlace(html, ev.Event);
      }

      html.WriteString(".");

      if (!string.IsNullOrEmpty(ev.Event.Description))
      {
        html.WriteString(" ");
        html.WriteRaw(ToInlineHtml(ev.Event.Description));
      }

      foreach (var related in ev.Related)
      {
        if (related.Type == EventType.Burial
          && IncludeBurialInformation
          && (related.Date.HasValue || related.Place != ev.Event.Place))
        {
          var pronoun = ev.Primary.Count == 1 ? ev.Primary[0].Pronoun() : "they";
          html.WriteString(" "
            + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pronoun)
            + (pronoun == "they" ? " were " : " was ")
            + "buried");
          AddDate(html, related.Date, includeDate);
          AddPlace(html, related, "in");
          html.WriteString(".");
        }
        else if (string.Equals(related.TypeString, "Diagnosis", StringComparison.OrdinalIgnoreCase))
        {
          var pronoun = ev.Primary.Count == 1 ? ev.Primary[0].Pronoun() : "they";
          html.WriteString(" "
            + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pronoun)
            + " had been diagnosed");
          if (related.Attributes.TryGetValue("Diagnosis", out var diagnosis))
          {
            html.WriteString(" with ");
            html.WriteString(diagnosis);
          }
          html.WriteString(".");
        }

        if (!string.IsNullOrEmpty(related.Description))
        {
          html.WriteString(" ");
          html.WriteRaw(ToInlineHtml(related.Description));
        }
      }
    }

    public static string ToInlineHtml(string markdown)
    {
      using (var writer = new StringWriter())
      {
        var renderer = new Markdig.Renderers.HtmlRenderer(writer);
        renderer.ImplicitParagraph = true;
        renderer.Render(Markdown.Parse(markdown));
        return writer.ToString();
      }
    }

    private void SetSubject(IEnumerable<Individual> individuals, bool isFamily)
    {
      PreviousSubject = individuals;
      _previousSubjectIsFamily = isFamily;
    }

    private void AddPlace(HtmlTextWriter html, Event ev, string word = "at")
    {
      if (!string.IsNullOrEmpty(ev.Organization?.Name))
      {
        html.WriteString(" " + word + " ");
        word = "at";
        html.WriteString(ev.Organization.Name);
      }

      var place = ev.Place ?? ev.Organization?.Place;
      if (!string.IsNullOrEmpty(place?.Names.FirstOrDefault()?.Name))
      {
        html.WriteString(" " + word + " ");
        html.WriteString(place.Names.First().Name);
      }
    }

    private void AddDate(HtmlTextWriter html, ExtendedDateRange date, bool includeDate, Individual ageIndividual = null)
    {
      if (!includeDate || !date.HasValue)
        return;

      html.WriteString(RenderDateRange(date, IncludeAges ? ageIndividual : null, MonthStyle));
    }

    public static string RenderDateRange(ExtendedDateRange date, Individual ageIndividual, string monthStyle = "MMMM")
    {
      string RenderDate(ExtendedDateRange range, bool start)
      {
        var date = (start ? range.Start : range.End);
        var format = "yyyy";
        if (date.Month.HasValue)
          format = monthStyle + " yyyy";
        if (date.Day.HasValue)
          format = monthStyle + " d, yyyy";
        var result = date.ToString(format);
        if (ageIndividual != null)
          result += GetAge(ageIndividual, range, !start);
        return result;
      };

      if (!date.HasValue)
        return null;

      if (date.Type == DateRangeType.Range)
      {
        if (date.Start.HasValue && date.End.HasValue)
          return " between " + RenderDate(date, true) + " and " + RenderDate(date, false);
        else if (date.Start.HasValue)
          return " after " + RenderDate(date, true);
        else //if (date.End.HasValue)
          return " before " + RenderDate(date, false);
      }
      else if (date.Type == DateRangeType.Period)
      {
        if (date.Start.HasValue && date.End.HasValue)
          return " from " + RenderDate(date, true) + " to " + RenderDate(date, false);
        else if (date.Start.HasValue)
          return " from " + RenderDate(date, true);
        else //if (date.End.HasValue)
          return " until " + RenderDate(date, false);
      }
      else
      {
        return (date.Start.Day.HasValue ? " on " : " in ") + RenderDate(date, true);
      }
    }

    private string Pronoun(IEnumerable<Individual> individuals)
    {
      if (individuals.Skip(1).Any())
        return "they";
      else
        return individuals.First().Pronoun();
    }

    private void AddNames(HtmlTextWriter html, IEnumerable<Individual> individuals, NameForm nameForm, ExtendedDateRange ageDate)
    {
      var first = true;

      if (nameForm.HasFlag(NameForm.Pronoun))
      {
        var matchCount = PreviousSubject == null ? 0 : individuals.Intersect(PreviousSubject).Count();
        if (matchCount > 0
          && !ageDate.HasValue
          && matchCount == PreviousSubject.Count()
          && matchCount == individuals.Count())
        {
          if (_previousSubjectIsFamily)
          {
            html.WriteString((nameForm.HasFlag(NameForm.Capitalize) ? "T" : "t") + "he family");
            return;
          }
          else if (matchCount == 1)
          {
            var pronoun = individuals.First().Pronoun();
            if (nameForm.HasFlag(NameForm.Capitalize))
              pronoun = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pronoun);
            html.WriteString(pronoun);
            return;
          }
        }

        nameForm = NameForm.AutoName;
      }

      foreach (var individual in individuals)
      {
        if (first)
          first = false;
        else
          html.WriteString(" and ");
        html.WriteStartElement("a");
        html.WriteAttributeString("href", "#" + individual.Id.Primary);

        var firstUsage = _existingReferences.Add(individual.Id.Primary);
        if (firstUsage && DirectAncestors?.Contains(individual.Id.Primary) == true)
          html.WriteAttributeString("style", "font-weight:bold");

        if (nameForm.HasFlag(NameForm.Auto))
        {
          if (firstUsage)
            nameForm = NameForm.Full;
          else
            nameForm = NameForm.Short;
        }

        if (nameForm.HasFlag(NameForm.All))
        {
          html.WriteString(individual.Name.Name);
          var aliases = individual.Names.Where(n => n.Type != NameType.Married)
            .SelectMany(n => new[] { n.Name.Remaining, n.Nickname })
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
          if (!string.IsNullOrEmpty(individual.Name.Remaining))
          {
            aliases.Remove(individual.Name.Remaining);
            foreach (var part in individual.Name.Remaining.Split(' '))
              aliases.Remove(part);
          }
          if (aliases.Count > 0)
          {
            html.WriteString(" (");
            var firstAlias = true;
            foreach (var alias in aliases)
            {
              if (firstAlias)
                firstAlias = false;
              else
                html.WriteString(", ");
              html.WriteString(alias);
            }
            html.WriteString(")");
          }
        }
        else
        {
          var name = individual.Names.OrderBy(n => {
            if (n.Type == NameType.Maiden && nameForm.HasFlag(NameForm.Maiden))
              return 0;
            else if (n.Type == NameType.Birth)
              return 1;
            else
              return 2;
          }).FirstOrDefault();
          if (nameForm.HasFlag(NameForm.Full))
          {
            html.WriteString(individual.Name.Name);
          }
          else // Short
          {
            html.WriteString(name.Nickname ?? name.Name.Remaining ?? name.GivenName);
          }
        }

        html.WriteEndElement();
        if (ageDate.HasValue && IncludeAges)
          html.WriteString(GetAge(individual, ageDate, false));
      }
    }

    internal static string GetAge(Individual i, ExtendedDateRange dateTime, bool useMax)
    {
      var birth = i.Events.FirstOrDefault(e => e.Type == EventType.Birth && e.Date.HasValue);
      if (birth != null && dateTime.HasValue
        && birth.Date.TryGetDiff(dateTime, out var minimum, out var maximum))
      {
        var span = useMax ? maximum : minimum;
        if (span.FullMonths < 1)
          return " (" + span.Days + " days)";
        else if (span.Years < 2)
          return " (" + span.FullMonths + " months)";
        else
          return " (" + span.Years + " years)";
      }
      return string.Empty;
    }

    [Flags]
    internal enum NameForm
    {
      Auto = 0x1,
      Full = 0x2,
      Short = 0x4,
      All = 0x8,
      Pronoun = 0x10,
      Capitalize = 0x20,
      Maiden = 0x40,
      AutoName = Auto | Full | Short,
      AutoPronoun = Auto | Full | Short | Pronoun,
      AutoPronounUpper = Auto | Full | Short | Pronoun | Capitalize,
    }
  }
}
