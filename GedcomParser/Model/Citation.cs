using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GedcomParser.Model
{
  public class Citation : IHasId, IHasAttributes, IHasNotes, IEquatable<Citation>
  {
    public Identifiers Id { get; } = new Identifiers();
    public string DuplicateOf { get; set; }

    public string Author { get; set; }
    public string Title { get; set; }
    public string PublicationTitle { get; set; }
    public string Pages { get; private set; }
    public Organization Publisher { get; set; }
    public Organization Repository { get; set; }
    public ExtendedDateRange DatePublished { get; set; }
    public ExtendedDateRange DateAccessed { get; set; }
    public string RecordNumber { get; set; }
    public Uri Url { get; set; }
    public string Doi { get; set; }
    public string Src { get; set; }

    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<Note> Notes { get; } = new List<Note>();

    public void SetPages(string page)
    {
      if (string.IsNullOrEmpty(page))
        return;
      if (Uri.TryCreate(page, UriKind.Absolute, out var uri)
        && (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "ftp"))
        Url = uri;
      else
      {
        var parts = page.Split(new[] { "; " }, StringSplitOptions.None).ToList();
        foreach (var part in parts.Where(p => p.IndexOf(": ") > 0))
        {
          var kvp = part.Split(new[] { ": " }, 2, StringSplitOptions.None);
          if (kvp[0] == "URL" && Uri.TryCreate(kvp[1], UriKind.Absolute, out uri)
            && uri.Scheme.StartsWith("http"))
          {
            Url = uri;
          }
          else
          {
            Attributes[kvp[0]] = kvp[1];
          }
        }

        parts = parts.Where(p => p.IndexOf(": ") <= 0).ToList();
        if (parts.Count > 0)
          Pages = string.Join("; ", parts);
      }

      if (!DatePublished.HasValue
        && Attributes.TryGetValue("Publication Date", out var pubDateStr)
        && ExtendedDateRange.TryParse(pubDateStr, out var pubDate))
      {
        Attributes.Remove("Publication Date");
        DatePublished = pubDate;
      }
      else if (!DatePublished.HasValue
        && Attributes.TryGetValue("Year", out var pubYearStr)
        && ExtendedDateRange.TryParse(pubYearStr, out pubDate))
      {
        Attributes.Remove("Year");
        DatePublished = pubDate;
      }

      if (Attributes.TryGetValue("Page", out var pageStr)
        && string.IsNullOrEmpty(Pages))
      {
        Attributes.Remove("Page");
        Pages = pageStr;
      }
    }

    public bool TryGetLink(out Link link)
    {
      link = null;
      if (!string.IsNullOrEmpty(Author)
        || !string.IsNullOrEmpty(PublicationTitle)
        || !string.IsNullOrEmpty(Pages)
        || Publisher != null
        || Repository != null
        || DatePublished.HasValue
        || DateAccessed.HasValue
        || !string.IsNullOrEmpty(RecordNumber)
        || !string.IsNullOrEmpty(Doi)
        || Url == null
        || Attributes.Count > 0
        || Notes.Count > 0)
        return false;
      link = new Link()
      {
        Url = Url,
        Description = Title
      };
      link.Id.AddRange(Id);
      return true;
    }

    public override int GetHashCode()
    {
      return ToEqualityString().GetHashCode();
    }

    public override bool Equals(object obj)
    {
      if (obj is Citation citation)
        return Equals(citation);
      return false;
    }

    public bool Equals(Citation other)
    {
      return this.ToEqualityString() == other.ToEqualityString();
    }

    public string ToEqualityString()
    {
      var builder = new StringBuilder();
      BuildEqualityString(builder, null);
      return builder.ToString().ToUpperInvariant();
    }

    public string GetPreferredId(Database db)
    {
      var builder = new StringBuilder();

      if ((Attributes.TryGetValue("Year", out var year)
        || Attributes.TryGetValue("Year(s)", out year)
        || Attributes.TryGetValue("Residence Date", out year))
        && year?.Length >= 4)
        builder.Append(year.Substring(0, 4));
      else if (Utilities.TryMatch(Pages + " " + Title, @"\b[1-2]\d{3}(s|-[1-2]\d{3})?\b", out var yearPattern))
      {
        builder.Append(yearPattern.Replace("s", "").Replace("-", ""));
      }
      else if (DatePublished.TryGetRange(out var start, out var _) && start.HasValue)
      {
        builder.Append(start.Value.ToString("yyyy"));
      }

      var parts = new List<string>();

      if (Attributes.TryGetValue("Census Place", out var censusPlace))
      {
        builder.Append("Census");
        if (Attributes.TryGetValue("Enumeration District", out var enumDistrict))
          builder.Append(enumDistrict.TrimStart('0'));
        parts.Add(censusPlace);
      }
      else if (Utilities.TryMatch(Title, @"\b(Census(es)?|Births?|Obituary|Marriage|Deaths?)\b", out var keyword))
      {
        builder.Append(keyword);
      }

      if (!string.IsNullOrEmpty(Author))
        parts.Add(Author.Split('.')[0]);
      if (Url != null)
      {
        var lastPart = default(string);
        var idx = Url.PathAndQuery.TrimEnd('/').LastIndexOfAny(new[] { '?', '/' });
        if (idx > 0)
        {
          lastPart = Url.PathAndQuery.Substring(idx + 1);
          if (Url.PathAndQuery[idx] == '?')
          {
            lastPart = string.Join("", lastPart.Split('&')
              .Select(k =>
              {
                var kvp = k.Split('=');
                return kvp.Length == 2 ? Uri.UnescapeDataString(kvp[1]) : "";
              }));
          }
          parts.Add(lastPart);
        }
      }
      if ((!string.IsNullOrEmpty(year) && Attributes.TryGetValue("Home in " + year, out var title))
        || Attributes.TryGetValue("Film Description", out title)
        || Attributes.TryGetValue("Book Title", out title))
        parts.Add(title);
      else if (!string.IsNullOrEmpty(Title))
        parts.Add(Title);
      if (!string.IsNullOrEmpty(Pages))
        parts.Add(Pages);

      foreach (var part in parts)
      {
        var remaining = Math.Min(10, 30 - builder.Length);
        if (remaining <= 0)
          break;
        Utilities.AddFirstLetters(part, 10, builder);
      }

      return builder.ToString();
    }

    public void BuildEqualityString(StringBuilder builder, Database db)
    {
      builder.Append(Author?.Trim())
        .Append(Title?.Trim())
        .Append(PublicationTitle?.Trim())
        .Append(Pages?.Trim())
        .Append(DatePublished.ToString("s"))
        .Append(DateAccessed.ToString("s"))
        .Append(RecordNumber?.Trim())
        .Append(Publisher?.Id.Primary)
        .Append(Repository?.Id.Primary)
        .Append(Src?.ToString())
        .Append(Url?.ToString())
        .Append(Doi?.Trim());
      Utilities.BuildEqualityString(this, builder);
    }
  }
}
