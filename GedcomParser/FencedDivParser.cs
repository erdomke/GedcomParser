using GedcomParser.Model;
using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GedcomParser
{
  internal class FencedDivExtension : IMarkdownExtension
  {
    private IEnumerable<ResolvedFamily> _families;
    public Database Database { get; }

    public FencedDivExtension(Database database)
    {
      Database = database;
    }

    public IEnumerable<ResolvedFamily> ResolvedFamilies()
    {
      if (_families == null)
        _families = ResolvedFamily.Resolve(Database.Families(), Database);
      return _families;
    }

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
      if (!pipeline.BlockParsers.Contains<FencedDivParser>())
        pipeline.BlockParsers.Add(new FencedDivParser(this));
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
      // Do nothing
    }
  }

  internal class FencedDivParser : FencedBlockParserBase<FencedDiv>
  {
    private readonly FencedDivExtension _extension;

    public FencedDivParser(FencedDivExtension extension)
    {
      OpeningCharacters = new[] { ':' };
      InfoPrefix = "";
      _extension = extension;
    }

    protected override FencedDiv CreateFencedBlock(BlockProcessor processor)
    {
      var codeBlock = new FencedDiv(this)
      {
        IndentCount = processor.Indent,
      };

      if (processor.TrackTrivia)
      {
        //codeBlock.LinesBefore = processor.UseLinesBefore();
        codeBlock.TriviaBefore = processor.UseTrivia(processor.Start - 1);
        codeBlock.NewLine = processor.Line.NewLine;
      }

      return codeBlock;
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
      var result = base.TryContinue(processor, block);
      if (result == BlockState.Continue && !processor.TrackTrivia)
      {
        var fence = (FencedDiv)block;
        // Remove any indent spaces
        var c = processor.CurrentChar;
        var indentCount = fence.IndentCount;
        while (indentCount > 0 && c.IsSpace())
        {
          indentCount--;
          c = processor.NextChar();
        }
      }

      return result;
    }

    public override bool Close(BlockProcessor processor, Block block)
    {
      var fencedDiv = (FencedDiv)block;
      if (fencedDiv.FirstClose)
      {
        fencedDiv.FirstClose = false;
        ParseInfoToAttributes(fencedDiv);
        if (fencedDiv.ClassNames.Contains("ged-report"))
        {
          var leafBlock = fencedDiv.FirstOrDefault() as LeafBlock;
          var builder = new StringBuilder();
          if (leafBlock.Lines.Lines != null)
          {
            var lines = leafBlock.Lines;
            var slices = lines.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
              if (i > 0)
                builder.AppendLine();
              builder.Append(slices[i].Slice.AsSpan());
            }
          }
          var code = builder.ToString();

          fencedDiv.Clear();
          var markdownBuilder = new StringBuilder();
          foreach (var family in _extension.ResolvedFamilies())
          {
            markdownBuilder.AppendLine();

            markdownBuilder.Append("## " + string.Join(" + ", family.Parents.Select(p => p.Name.Surname)));
            markdownBuilder.AppendLine();

            var familyMembers = family.Parents.Concat(family.Children).ToList();
            foreach (var familyEvent in family.Events.OrderBy(e => e.Date.Start))
            {
              markdownBuilder.AppendLine();
              var individual = _extension.Database.WhereUsed(familyEvent).OfType<Individual>().Intersect(familyMembers).FirstOrDefault();
              if (individual == null)
                markdownBuilder.Append($"- {familyEvent.Date:s}, {familyEvent.Type}, {familyEvent.Place}");
              else
                markdownBuilder.Append($"- {familyEvent.Date:s}, {familyEvent.Type} of {individual.Name}, {familyEvent.Place}");
            }
          }

          var childProcessor = processor.CreateChild();
          fencedDiv.IsOpen = true;
          childProcessor.Open(fencedDiv);
          var mdLines = markdownBuilder.ToString().Split(new string[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
          foreach (var line in mdLines)
            childProcessor.ProcessLine(new StringSlice(line));
          childProcessor.Close(fencedDiv);
        }
      }
      return base.Close(processor, block);
    }

    private void ParseInfoToAttributes(FencedDiv fencedDiv)
    {
      var info = fencedDiv.Info.Trim();
      var inBrackets = false;
      var attrName = default(string);
      var quotedAttr = false;
      var start = 0;
      for (var i = 0; i < info.Length; i++)
      {
        if (inBrackets)
        {
          if (attrName == null)
          {
            if (info[i] == ' '
              || info[i] == '{'
              || info[i] == '}')
            {
              if (i > start)
              {
                var value = info.Substring(start, i - start);
                if (value.StartsWith("."))
                  fencedDiv.ClassNames.Add(value.TrimStart('.'));
                else if (value.StartsWith("#"))
                  fencedDiv.Attributes["id"] = value.TrimStart('#');
                else
                  fencedDiv.Attributes[value] = "";
              }
              start = i + 1;
              inBrackets = info[i] != '}';
            }
            else if (info[i] == '=')
            {
              attrName = info.Substring(start, i - start);
              start = i + 1;
              if (start < info.Length && info[start] == '"')
              {
                start++;
                quotedAttr = true;
              }
            }
          }
          else
          {
            if ((quotedAttr && info[i] == '"')
              || (!quotedAttr && info[i] == ' '))
            {
              fencedDiv.Attributes[attrName] = info.Substring(start, i - start);
              attrName = null;
              quotedAttr = false;
              start = i + 1;
            }
          }
        }
        else
        {
          if (info[i] == ' '
            || info[i] == '{'
            || info[i] == '}')
          {
            if (i > start)
              fencedDiv.ClassNames.Add(info.Substring(start, i - start));
            start = i + 1;
            inBrackets = info[i] == '{';
          }
        }
      }

      if (info.Length > start)
      {
        var value = info.Substring(start);
        if (!inBrackets || (inBrackets && value.StartsWith(".")))
          fencedDiv.ClassNames.Add(value.TrimStart('.'));
        else if (value.StartsWith("#"))
          fencedDiv.Attributes["id"] = value.TrimStart('#');
        else
          fencedDiv.Attributes[value] = "";
      }
    }
  }

  internal class FencedDiv : ContainerBlock, IFencedBlock
  {
    public FencedDiv(BlockParser parser) : base(parser)
    {
    }

    public bool FirstClose { get; set; } = true;
    public HashSet<string> ClassNames { get; } = new HashSet<string>();
    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
    public int IndentCount { get; set; }
    public char FencedChar { get; set; }
    public int OpeningFencedCharCount { get; set; }
    public StringSlice TriviaAfterFencedChar { get; set; }
    public string Info { get; set; }
    public StringSlice UnescapedInfo { get; set; }
    public StringSlice TriviaAfterInfo { get; set; }
    public string Arguments { get; set; }
    public StringSlice UnescapedArguments { get; set; }
    public StringSlice TriviaAfterArguments { get; set; }
    public NewLine InfoNewLine { get; set; }
    public StringSlice TriviaBeforeClosingFence { get; set; }
    public int ClosingFencedCharCount { get; set; }
  }
}
