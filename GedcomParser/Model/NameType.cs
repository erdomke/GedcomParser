namespace GedcomParser.Model
{
    public enum NameType
    {
        /// <summary>
        /// A value not listed here; should have a PHRASE substructur
        /// </summary>
        Other,
        /// <summary>
        /// Also known as, alias, etc.
        /// </summary>
        Aka,
        /// <summary>
        /// Name given at or near birth.
        /// </summary>
        Birth,
        /// <summary>
        /// Name assumed at the time of immigration.
        /// </summary>
        Immigrant,
        /// <summary>
        /// Maiden name, name before first marriage.
        /// </summary>
        Maiden,
        /// <summary>
        /// Married name, assumed as part of marriage.
        /// </summary>
        Married,
        /// <summary>
        /// Name used professionally (pen, screen, stage name).
        /// </summary>
        Professional,
    }
}
