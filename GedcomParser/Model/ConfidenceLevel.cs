namespace GedcomParser.Model
{
    public enum ConfidenceLevel
    {
        /// <summary>
        /// Unreliable evidence or estimated data
        /// </summary>
        VeryLow = 0,
        /// <summary>
        /// Questionable reliability of evidence (interviews, census, oral genealogies, or potential for bias, such as an autobiography)
        /// </summary>
        Low = 1,
        /// <summary>
        /// Secondary evidence, data officially recorded sometime after the event
        /// </summary>
        Normal = 2,
        /// <summary>
        /// Direct and primary evidence used, or by dominance of the evidence
        /// </summary>
        High = 3,
        VeryHigh = 4
    }
}
