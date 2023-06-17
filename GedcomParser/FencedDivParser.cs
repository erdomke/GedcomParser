﻿using GedcomParser.Model;
using GedcomParser.Renderer;
using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GedcomParser
{
  internal class FencedDivExtension : IMarkdownExtension
  {
    private IEnumerable<ResolvedFamily> _families;

    public Database Database { get; }
    public IGraphics Graphics { get; set; }

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
          var builder = new StringBuilder();
          BuildLines(fencedDiv, builder);
          var options = ReportOptions.Parse(builder.ToString());

          var childProcessor = processor.CreateChild();
          childProcessor.Open(fencedDiv.Parent);

          var groups = FamilyGroup.Create(_extension.ResolvedFamilies(), options);
          foreach (var group in groups)
          {
            childProcessor.ProcessLine(new StringSlice(""));
            var title = "## " + group.Title;
            if (group.Families.Count == 1)
              title += $" {{#{group.Families[0].Id.Primary}}}";
            childProcessor.ProcessLine(new StringSlice(title));
            childProcessor.ProcessLine(new StringSlice(""));

            if (group.Families.Count > 1)
            {
              foreach (var family in group.Families)
                childProcessor.ProcessLine(new StringSlice($"<a id='{family.Id.Primary}'></a>"));
              childProcessor.ProcessLine(new StringSlice(""));
            }

            var baseDirectory = Path.GetDirectoryName(_extension.Database.BasePath);

            //var familyRender = new FamilyRenderer()
            //{
            //  Graphics = _extension.Graphics
            //};
            //childProcessor.ProcessLine(new StringSlice(familyRender.Render(group.Families
            //  , Path.GetDirectoryName(_extension.Database.BasePath)).ToString()));
            //childProcessor.ProcessLine(new StringSlice(""));
            var decendantRenderer = new DecendantLayout()
            {
              Graphics = _extension.Graphics
            };
            childProcessor.ProcessLine(new StringSlice(decendantRenderer.Render(group.Families
              , baseDirectory).ToString()));
            childProcessor.ProcessLine(new StringSlice(""));

            var timelineRenderer = new TimelineRenderer()
            {
              Graphics = _extension.Graphics
            };
            childProcessor.ProcessLine(new StringSlice(timelineRenderer.Render(group.Families
              , baseDirectory).ToString()));
            childProcessor.ProcessLine(new StringSlice(""));

            var mapRenderer = new MapRenderer();
            if (mapRenderer.TryRender(group.Families, baseDirectory, out var map))
            {
              childProcessor.ProcessLine(new StringSlice(map.ToString()));
              childProcessor.ProcessLine(new StringSlice(""));
            }

            foreach (var resolvedEvent in group.Families.SelectMany(f => f.Events)
              .Where(e => e.Event.Date.HasValue)
              .OrderBy(e => e.Event.Date))
            {
              childProcessor.ProcessLine(new StringSlice($"- <date>{resolvedEvent.Event.Date:yyyy MMM d}</date>: {resolvedEvent.Description()}"));
            }
          }

          childProcessor.Close(fencedDiv);
        }
      }
      return base.Close(processor, block);
    }

    private void BuildLines(Block block, StringBuilder builder)
    {
      if (block is LeafBlock leafBlock)
      {
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
      }
      else if (block is ContainerBlock container)
      {
        foreach (var child in container)
          BuildLines(child, builder);
      }
      else
      {
        throw new NotSupportedException();
      }
    }

    private class FamilyGroup
    {
      public string Title { get; set; }
      public List<ResolvedFamily> Families { get; } = new List<ResolvedFamily>();

      public static IEnumerable<FamilyGroup> Create(IEnumerable<ResolvedFamily> resolvedFamilies, ReportOptions options)
      {
        var xref = new Dictionary<string, ResolvedFamily>();
        foreach (var family in resolvedFamilies)
        {
          foreach (var id in family.Id)
            xref.Add(id, family);
        }

        var result = options.Groups
          .Select(g =>
          {
            var resolved = new FamilyGroup()
            {
              Title = g.Title
            };
            resolved.Families.AddRange(g.Ids
              .Select(i => xref.TryGetValue(i, out var f) ? f : null)
              .Where(f => f != null)
              .Distinct()
              .OrderBy(f => f.StartDate));
            if (string.IsNullOrEmpty(resolved.Title))
              resolved.Title = string.Join(" + ", resolved.Families.SelectMany(f => f.Parents).Select(p => p.Name.Surname).Distinct());
            return resolved;
          })
          .ToList();

        foreach (var id in result.SelectMany(g => g.Families).SelectMany(f => f.Id))
          xref.Remove(id);

        foreach (var family in xref.Values)
        {
          var resolved = new FamilyGroup()
          {
            Title = string.Join(" + ", family.Parents.Select(p => p.Name.Surname))
          };
          resolved.Families.Add(family);
          result.Add(resolved);
        }

        return result.OrderByDescending(g => g.Families.First().StartDate).ToList();
      }
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

  internal class FencedDiv : LeafBlock, IFencedBlock
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
