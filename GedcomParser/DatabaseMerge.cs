using GedcomParser.Model;
using SixLabors.Fonts.Tables.AdvancedTypographic;
using Svg;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GedcomParser
{
  internal class DatabaseMerge
  {
    private readonly List<Match> _matches = new List<Match>();
    private readonly Database _source;
    private readonly Database _target;
    private readonly List<Merge> _merges = new List<Merge>();

    public DatabaseMerge(Database source, Database target)
    {
      _source = source;
      _target = target;
    }

    public void Add(string source, string target)
    {
      AddMatch(new Match(source, target));
    }

    public void Report(string path)
    {
      using (var writer = new StreamWriter(path))
      using (var html = new HtmlTextWriter(writer))
      {
        html.WriteStartElement("html");

        html.WriteStartElement("head");
        html.WriteElementString("style", @"pre {
 white-space: pre-wrap;
}
td {
  vertical-align: top;
}");
        html.WriteEndElement();

        html.WriteStartElement("body");
        html.WriteStartElement("table");
        foreach (var merge in _merges
          .OrderBy(m => m.Family ? 1 : 0)
          .ThenBy(m => m.MergeId))
        {
          html.WriteStartElement("tr");
          html.WriteStartElement("td");
          html.WriteElementString("pre", merge.Source);
          html.WriteEndElement();
          html.WriteStartElement("td");
          html.WriteElementString("pre", merge.Target);
          html.WriteEndElement();
          html.WriteStartElement("td");
          html.WriteElementString("pre", merge.Merged);
          html.WriteEndElement();
          html.WriteEndElement();
        }
        html.WriteEndElement();
        html.WriteEndElement();
        html.WriteEndElement();
      }
    }

    public void Process()
    {
      for (var i = 0; i < _matches.Count; i++)
      {
        if (_source.TryGetValue(_matches[i].Source, out Individual sourceIndividual))
        {
          if (string.IsNullOrEmpty(_matches[i].Target))
            AddIndividual(sourceIndividual);
          else if (_target.TryGetValue(_matches[i].Target, out Individual targetIndividual))
            ProcessMatch(sourceIndividual, targetIndividual);
        }
        else if (_source.TryGetValue(_matches[i].Source, out Family sourceFamily))
        {
          if (string.IsNullOrEmpty(_matches[i].Target))
            AddFamily(sourceFamily);
          else if (_target.TryGetValue(_matches[i].Target, out Family targetFamily))
            ProcessMatch(sourceFamily, targetFamily);
        }
      }
    }

    private void ProcessMatch(Individual source, Individual target)
    {
      var writer = new YamlWriter();
      var sourceYaml = writer.Visit(source).ToYamlString();
      var targetYaml = writer.Visit(target).ToYamlString();

      Console.WriteLine($"Processing match {source.Name.Name} -> {target.Name.Name}");
      target.Names.AddRange(source.Names
        .Where(s => !target.Names
          .Any(t => string.Equals(t.Name.Name, s.Name.Name, StringComparison.OrdinalIgnoreCase))));
      if (target.Picture == null && source.Picture != null)
        target.Picture = ProcessMedia(source.Picture);
      MergeAttributes(source, target);
      MergeCitations(source, target);
      MergeEvents(source, target);
      MergeLinks(source, target);
      MergeMedia(source, target);
      MergeNotes(source, target);

      var sourceGroup = GetLinks(_source, source, FamilyLinkType.Parent, FamilyLinkType.Parent);
      var targetGroup = GetLinks(_target, target, FamilyLinkType.Parent, FamilyLinkType.Parent);
      var newMatches = GenerateMatches(sourceGroup, targetGroup);
      sourceGroup = GetLinks(_source, source, FamilyLinkType.Child, FamilyLinkType.Parent);
      targetGroup = GetLinks(_target, target, FamilyLinkType.Child, FamilyLinkType.Parent);
      newMatches.AddRange(GenerateMatches(sourceGroup, targetGroup));

      foreach (var match in newMatches)
      {
        AddMatch(new Match(match.Individual.Id.Primary, null));
        AddMatch(new Match(match.Family, null));
      }

      _merges.Add(new Merge(sourceYaml, targetYaml, writer.Visit(target).ToYamlString(), target.Id.Primary, false));
    }

    private void AddIndividual(Individual source)
    {
      AddEvents(source.Events);
      if (_target.ContainsId(source.Id.Primary))
        source.Id.Replace(Guid.NewGuid().ToString("N"));
      foreach (var match in GetLinks(_source, source, FamilyLinkType.Child, FamilyLinkType.Parent))
      {
        Console.WriteLine($"Found new person {match.Individual.Name}.");
        AddMatch(new Match(match.Individual.Id.Primary, null));
        AddMatch(new Match(match.Family, null));
      }
      _target.Add(source);
    }

    private void AddFamily(Family source)
    {
      AddEvents(source.Events);
      if (_target.ContainsId(source.Id.Primary))
        source.Id.Replace(Guid.NewGuid().ToString("N"));
      _target.Add(source);
      foreach (var link in _source.FamilyLinks(source, FamilyLinkType.Other)
        .OrderBy(l => l.Order))
      {
        var id = link.Individual;
        var match = _matches.FirstOrDefault(m => m.Source == link.Individual && !string.IsNullOrEmpty(m.Target));
        if (match != null)
          id = match.Target;
        _target.Add(new FamilyLink()
        {
          Family = source.Id.Primary,
          Individual = id,
          Type = link.Type
        });
      }
    }

    private void AddEvents(IEnumerable<Event> source)
    {
      foreach (var ev in source)
      {
        ev.Place = MergePlace(ev.Place);
        ev.Organization = MergeOrganization(ev.Organization);
      }
    }

    private bool AddMatch(Match match)
    {
      if (_matches.Any(m => m.Source == match.Source))
        return false;
      _matches.Add(match);
      return true;
    }

    private List<IndividualMatcher> GenerateMatches(IEnumerable<IndividualMatcher> sourceGroup, IEnumerable<IndividualMatcher> targetGroup)
    {
      var newIndividuals = new List<IndividualMatcher>();
      foreach (var sourceMatch in sourceGroup)
      {
        var matchFound = false;
        foreach (var targetMatch in targetGroup)
        {
          var score = sourceMatch.MatchScore(targetMatch);
          if (score >= 2)
          {
            matchFound = true;
            if (AddMatch(new Match(sourceMatch.Individual.Id.Primary, targetMatch.Individual.Id.Primary)))
              Console.WriteLine($"Matched {sourceMatch.Individual.Name} with {targetMatch.Individual.Name}.");
            AddMatch(new Match(sourceMatch.Family, targetMatch.Family));
          }
        }

        if (!matchFound)
        {
          Console.WriteLine($"Found new person {sourceMatch.Individual.Name}.");
          newIndividuals.Add(sourceMatch);
        }
      }
      return newIndividuals;
    }

    private IEnumerable<IndividualMatcher> GetLinks(Database db, Individual individual, FamilyLinkType first, FamilyLinkType second)
    {
      return db.FamilyLinks(individual, first)
        .SelectMany(l => db.FamilyLinks(l.Family, second))
        .Where(l => !individual.Id.Contains(l.Individual))
        .Select(l => db.TryGetValue(l.Individual, out Individual related) ? new IndividualMatcher(related, l.Family, l.Type) : null)
        .Where(i => i != null);
    }

    private void MergeEvents(IHasEvents source, IHasEvents target)
    {
      foreach (var eventGroup in source.Events.GroupBy(s => s.TypeName, StringComparer.OrdinalIgnoreCase))
      {
        var existing = target.Events.Where(t => t.TypeName == eventGroup.Key).ToList();
        if (existing.Count == 0)
        {
          AddEvents(eventGroup);
          target.Events.AddRange(eventGroup);
        }
        else if (existing.Count == 1 && eventGroup.Count() == 1)
        {
          MergeEvent(eventGroup.First(), existing[0]);
        }
      }
    }

    private void MergeEvent(Event source, Event target)
    {
      if (source.Date.HasValue)
      {
        if (!target.Date.HasValue
          || source.Date.ToString("yyyyMMdd").StartsWith(target.Date.ToString("yyyyMMdd")))
        {
          target.Date = source.Date;
        }
      }
      if (target.Place == null && source.Place != null)
        target.Place = MergePlace(source.Place);
      if (target.Organization == null && source.Organization != null)
        target.Organization = MergeOrganization(source.Organization);
      target.Description = target.Description ?? source.Description;
      MergeAttributes(source, target);
      MergeCitations(source, target);
      MergeLinks(source, target);
      MergeMedia(source, target);
      MergeNotes(source, target);
    }

    private Place MergePlace(Place source)
    {
      if (source == null)
        return null;
      if (_target.TryGetValue(source.Id.Primary, out Place target))
        return target;
      _target.Add(source);
      return source;
    }

    private Organization MergeOrganization(Organization source)
    {
      if (source == null)
        return null;
      if (_target.TryGetValue(source.Id.Primary, out Organization target))
        return target;
      _target.Add(source);
      return source;
    }

    private void MergeAttributes(IHasAttributes source, IHasAttributes target)
    {
      foreach (var attr in source.Attributes.Where(s => !target.Attributes.ContainsKey(s.Key)))
        target.Attributes[attr.Key] = attr.Value;
    }

    private void MergeCitations(IHasCitations source, IHasCitations target)
    {
      // Do nothing, ... for now.
    }

    private void MergeLinks(IHasLinks source, IHasLinks target)
    {
      target.Links.AddRange(source.Links
        .Where(s => !target.Links
          .Any(t => t.Url.ToString() == s.Url.ToString())));
    }

    private void MergeMedia(IHasMedia source, IHasMedia target)
    {
      target.Media.AddRange(source.Media
        .Where(s => !target.Media
          .Any(t => t.Src == s.Src && t.Content == s.Content && t.Description == s.Description))
        .Select(ProcessMedia));
    }

    private Media ProcessMedia(Media source)
    {
      source.Place = MergePlace(source.Place);
      foreach (var child in source.Children)
        ProcessMedia(child);
      return source;
    }

    private void MergeNotes(IHasNotes source, IHasNotes target)
    {
      target.Notes.AddRange(source.Notes
        .Where(s => !target.Notes
          .Any(t => string.Equals(t.Text, s.Text, StringComparison.OrdinalIgnoreCase))));
    }

    private void ProcessMatch(Family source, Family target)
    {
      var writer = new YamlWriter();
      var sourceYaml = writer.Visit(source, _source).ToYamlString();
      var targetYaml = writer.Visit(target, _target).ToYamlString();

      Console.WriteLine($"Processing match {source.Id.Primary} -> {target.Id.Primary}");
      var sourceGroup = Children(_source, source, true);
      var targetGroup = Children(_target, target, false);
      var newMatches = GenerateMatches(sourceGroup, targetGroup);
      foreach (var newMatch in newMatches)
      {
        AddIndividual(newMatch.Individual);
        _target.Add(new FamilyLink()
        {
          Family = target.Id.Primary,
          Individual = newMatch.Individual.Id.Primary,
          Type = newMatch.LinkType
        });
      }
      MergeAttributes(source, target);
      MergeCitations(source, target);
      MergeEvents(source, target);
      MergeLinks(source, target);
      MergeMedia(source, target);
      MergeNotes(source, target);

      _merges.Add(new Merge(sourceYaml, targetYaml, writer.Visit(target, _target).ToYamlString(), target.Id.Primary, true));
    }

    private IEnumerable<IndividualMatcher> Children(Database database, Family family, bool source)
    {
      return database.FamilyLinks(family, FamilyLinkType.Child)
        .Where(l => !_matches.Any(m => (source && m.Source == l.Individual) || (!source && m.Target == l.Individual)))
        .Select(l => database.TryGetValue(l.Individual, out Individual related) ? new IndividualMatcher(related, l.Family, l.Type) : null)
        .Where(i => i != null);
    }

    private record Match(string Source, string Target);

    private record Merge(string Source, string Target, string Merged, string MergeId, bool Family);

    private class IndividualMatcher
    {
      private HashSet<string> _givenNames;
      private HashSet<string> _surnames;
      private HashSet<string> _birthDates;
      private HashSet<string> _deathDates;
      private int _birthYear;
      private int _deathYear;
      
      public Individual Individual { get; }
      public string Family { get; }
      public FamilyLinkType LinkType { get; }

      public IndividualMatcher(Individual individual, string family, FamilyLinkType linkType)
      {
        Individual = individual;
        Family = family;
        LinkType = linkType;

        _surnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _surnames.UnionWith(individual.Names
          .SelectMany(n => new[] { n.Name.Surname, n.Surname })
          .Where(n => !string.IsNullOrEmpty(n))
          .Select(n => n.Trim()));
        _givenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _givenNames.UnionWith(individual.Names
          .SelectMany(n =>
          {
            var names = new List<string>();
            if (!string.IsNullOrEmpty(n.Name.Remaining))
            {
              names.Add(n.Name.Remaining);
              names.AddRange(n.Name.Remaining.Split(' '));
            }
            if (!string.IsNullOrEmpty(n.GivenName))
            {
              names.Add(n.GivenName);
              names.AddRange(n.GivenName.Split(' '));
            }
            if (!string.IsNullOrEmpty(n.Nickname))
            {
              names.Add(n.Nickname);
              names.AddRange(n.Nickname.Split(' '));
            }
            return names;
          })
          .Select(n => n.Trim()));

        _birthDates = new HashSet<string>();
        var birthDate = individual.BirthDate;
        if (birthDate.Type == DateRangeType.Date)
        {
          if (birthDate.Start.Day.HasValue)
            _birthDates.Add(birthDate.Start.ToString("yyyyMMdd"));
          if (birthDate.Start.Month.HasValue)
            _birthDates.Add(birthDate.Start.ToString("yyyyMM"));
          _birthDates.Add(birthDate.Start.ToString("yyyy"));
          _birthYear = birthDate.Start.Year;
        }
        _deathDates = new HashSet<string>();
        var deathDate = individual.DeathDate;
        if (deathDate.Type == DateRangeType.Date)
        {
          if (deathDate.Start.Day.HasValue)
            _deathDates.Add(deathDate.Start.ToString("yyyyMMdd"));
          if (deathDate.Start.Month.HasValue)
            _deathDates.Add(deathDate.Start.ToString("yyyyMM"));
          _deathDates.Add(deathDate.Start.ToString("yyyy"));
          _deathYear = deathDate.Start.Year;
        }
      }

      public int MatchScore(IndividualMatcher other)
      {
        var score = 0;
        if (_givenNames.Overlaps(other._givenNames))
          score++;
        if (_surnames.Overlaps(other._surnames))
          score++;

        var sameBirth = _birthDates.Intersect(other._birthDates).OrderByDescending(d => d.Length).FirstOrDefault();
        if (sameBirth?.Length == 8)
          score += 2;
        else if (!string.IsNullOrEmpty(sameBirth))
          score++;
        else if (_birthYear > 0
          && other._birthYear > 0
          && Math.Abs(_birthYear - other._birthYear) > 10)
          score -= 2;

        var sameDeath = _deathDates.Intersect(other._deathDates).OrderByDescending(d => d.Length).FirstOrDefault();
        if (sameDeath?.Length == 8)
          score += 2;
        else if (!string.IsNullOrEmpty(sameDeath))
          score++;
        else if (_deathYear > 0
          && other._deathYear > 0
          && Math.Abs(_deathYear - other._deathYear) > 10)
          score -= 2;
        return score;
      }
    }
  }
}
