using GedcomParser.Model;
using GedcomParser.Renderer;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GedcomParser
{
  internal static class Utilities
  {
    public static string Checksum(this IHasId hasId, Database db)
    {
      using (var md5 = MD5.Create())
      {
        var builder = new StringBuilder();
        hasId.BuildEqualityString(builder, db);
        return Base32.ToBase32String(md5.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
      }
    }

    public static void AddFirstLetters(string value, int count, StringBuilder builder, bool includeNumbers = false)
    {
      if (value == null)
        return;

      var letters = 0;
      for (var i = 0; i < value.Length && letters < count; i++)
      {
        if (char.IsLetter(value[i])
          || (includeNumbers && char.IsNumber(value[i])))
        {
          letters++;
          builder.Append(value[i]);
        }
      }
    }

    public static bool TryMatch(string input, string pattern, out string value)
    {
      var match = Regex.Match(input, pattern);
      value = match.Value;
      return match.Success;
    }

    public static void BuildEqualityString(object primaryObject, StringBuilder builder)
    {
      if (primaryObject is IHasAttributes attributes)
      {
        foreach (var attr in attributes.Attributes)
          builder.Append(attr.Key).Append(attr.Value);
      }

      if (primaryObject is IHasCitations citations)
      {
        foreach (var citation in citations.Citations)
          builder.Append(citation.Id.Primary);
      }

      if (primaryObject is IHasLinks links)
      {
        foreach (var link in links.Links)
          builder.Append(link.Url?.ToString()).Append(link.Description);
      }

      if (primaryObject is IHasMedia hasMedia)
      {
        foreach (var media in hasMedia.Media)
          builder.Append(media.Src);
      }

      if (primaryObject is IHasNotes notes)
      {
        foreach (var note in notes.Notes)
          builder.Append(note.Text);
      }
    }

    public static void WriteStartSection(this HtmlTextWriter html, ISection section, RenderState state)
    {
      html.WriteStartElement("section");
      if (section is TableOfContentsSection)
        html.WriteAttributeString("class", "toc");

      if (state.RestartPageNumbers)
      {
        html.WriteStartElement("a");
        html.WriteAttributeString("class", "startPageNumber");
        html.WriteEndElement();
        state.RestartPageNumbers = false;
      }

      html.WriteStartElement("h2");
      html.WriteAttributeString("id", section.Id);
      html.WriteString(section.Title);
      html.WriteEndElement();
    }
  }
}
