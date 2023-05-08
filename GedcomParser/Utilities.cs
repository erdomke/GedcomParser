using System.Text;
using System.Text.RegularExpressions;

namespace GedcomParser
{
  internal class Utilities
  {
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

  }
}
