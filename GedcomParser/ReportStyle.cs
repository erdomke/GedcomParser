namespace GedcomParser
{
  internal class ReportStyle
  {
    public string FontName { get; set; } = "Calibri";
    public double BaseFontSize { get; set; } = 12;

    public static ReportStyle Default { get; } = new ReportStyle();
  }
}
