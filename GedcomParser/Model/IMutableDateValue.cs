using System;

namespace GedcomParser
{
    internal interface IMutableDateValue
    {
        void SetPhrase(string value);
        void SetTime(TimeSpan timeSpan, bool isUtc);
    }
}
