﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GedcomParser.Renderer
{
  internal class TableOfContentsSection : ISection
  {
    public List<ISection> Sections { get; } = new List<ISection>();
    public string Title => "Table of Contents";
    public string Id => "TOC";

    public void Render(HtmlTextWriter html, ReportRenderer renderer, RenderState state)
    {
      html.WriteStartSection(this, state);
      html.WriteStartElement("div");
      foreach (var section in Sections)
      {
        html.WriteStartElement("a");
        html.WriteAttributeString("class", "toc-link");
        html.WriteAttributeString("href", "#" + section.Id);
        html.WriteString(section.Title);
        html.WriteStartElement("div");
        html.WriteAttributeString("class", "filler");
        html.WriteEndElement();
        html.WriteEndElement();


        html.WriteEndElement();
      }
      html.WriteEndElement();
      html.WriteEndElement();
    }
  }
}
