using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.NetworkInformation;
using System.Xml;
using System.Xml.Linq;
using YamlDotNet.RepresentationModel;

namespace GedcomParser
{
  public class GedcomLoader
  {
    private Dictionary<string, GStructure> _sources = new Dictionary<string, GStructure>();
    private HashSet<Citation> _citations = new HashSet<Citation>();
    private Dictionary<string, Place> _places = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Organization> _orgs = new Dictionary<string, Organization>(StringComparer.OrdinalIgnoreCase);

    private void GetPaths(GStructure structure, string path, HashSet<string> paths)
    {
      if (structure.Children().Any())
      {
        foreach (var child in structure.Children())
        {
          GetPaths(child, path + "/" + structure.Tag, paths);
        }
      }
      else
      {
        paths.Add(path + "/" + structure.Tag);
      }
    }

    public void Load(Database database, GStructure structure)
    {
      _sources = structure.Children("SOUR")
        .Where(s => !string.IsNullOrEmpty(s.Id))
        .ToDictionary(s => s.Id);

      foreach (var child in structure.Children("OBJE"))
        database.Add(Media(child, database));
      
      foreach (var child in structure.Children("INDI"))
      {
        var individual = Individual(child, database);
        database.Add(individual);
        foreach (var familyChild in child.Children("FAMC"))
        {
          database.Add(new FamilyLink()
          {
            Individual = individual.Id.Primary,
            Family = familyChild.Pointer,
            Type = Enum.Parse<FamilyLinkType>((string)familyChild.Child("PEDI") ?? "Birth", true)
          });
        }
      }

      foreach (var child in structure.Children("FAM"))
      {
        var family = Family(child, database);
        database.Add(family);

        foreach (var rel in child.Children("HUSB")
            .Select(p => new FamilyLink()
            {
              Individual = p.Pointer,
              Type = FamilyLinkType.Father
            })
            .Concat(child.Children("WIFE")
            .Select(p => new FamilyLink()
            {
              Individual = p.Pointer,
              Type = FamilyLinkType.Mother
            })).Concat(child.Children("ASSO")
            .Select(p => new FamilyLink()
            {
              Individual = p.Pointer,
              Type = GetRelationshipType((string)child.Child("ROLE")
                        ?? (string)child.Child("RELA"))
            })))
        {
          rel.Family = family.Id.Primary;
          database.Add(rel);
        }
      }
    }

    public Individual Individual(GStructure structure, Database db)
    {
      var result = new Individual();
      result.Id.Add(structure.Id);
      switch ((string)structure.Child("SEX") ?? "")
      {
        case "M":
          result.Sex = Sex.Male;
          break;
        case "F":
          result.Sex = Sex.Female;
          break;
        case "X":
          result.Sex = Sex.Other;
          break;
      }
      result.Names.AddRange(structure
          .Children("NAME")
          .Select(s => IndividualName(s, db)));
      result.Events.AddRange(structure
          .Children()
          .Where(c => TryGetEventType(c.Tag, out var _))
          .Select(e => Event(e, db)));
      AddCommonFields(structure, result, db);
      return result;
    }

    public IndividualName IndividualName(GStructure structure, Database db)
    {
      var result = new IndividualName
      {
        Name = (PersonName)structure
      };

      var type = structure.Child("TYPE");
      if (Enum.TryParse<NameType>((string)type ?? "Birth", true, out var nameType))
      {
        result.Type = nameType;
        result.TypeString = (string)type?.Child("PHRASE");
        if (!string.IsNullOrEmpty(result.TypeString))
          result.Type = NameType.Other;
      }
      else
      {
        result.TypeString = (string)type;
        result.Type = NameType.Other;
      }
      result.NamePrefix = (string)structure.Child("NPFX");
      result.GivenName = (string)structure.Child("GIVN");
      result.Nickname = (string)structure.Child("NICK");
      result.SurnamePrefix = (string)structure.Child("SPFX");
      result.Surname = (string)structure.Child("SURN");
      result.NameSuffix = (string)structure.Child("NSFX");
      foreach (var tran in structure.Children("TRAN"))
      {
        result.Translations.Add((string)tran.Child("LANG"), IndividualName(tran, db));
      }
      AddCommonFields(structure, result, db);
      return result;
    }

    public Family Family(GStructure structure, Database db)
    {
      var result = new Family();
      result.Id.Add(structure.Id);
      result.Events.AddRange(structure
          .Children()
          .Where(c => TryGetEventType(c.Tag, out var _))
          .Select(e => Event(e, db)));
      AddCommonFields(structure, result, db);
      return result;
    }


    public Event Event(GStructure structure, Database db)
    {
      var result = new Event();
      if (structure.Tag == "EVEN")
        result.TypeString = (string)structure.Child("TYPE");
      else if (TryGetEventType(structure.Tag, out var type))
        result.Type = type;
      else
        throw new ArgumentException($"Structure {structure.Tag} does not represent a valid event.");
      if (structure.Child("DATE") != null
          && structure.Child("DATE").TryGetDateRange(out var dateRange))
        result.Date = dateRange;
      else
        result.Date = default;
      if (structure.Child("PLAC") != null)
        result.Place = Place(structure.Child("PLAC"), db);
      AddCommonFields(structure, result, db);
      return result;
    }

    public Place Place(GStructure structure, Database db)
    {
      var result = new Place();
      var name = ((string)structure)?.Trim(' ', ',').Replace(" ,", ",").Replace("  ", " ");
      if (string.IsNullOrEmpty(name))
        return null;
      else if (_places.TryGetValue(name, out var place))
        return place;
      else
        _places.Add(name, result);

      result.Id.Add(Guid.NewGuid().ToString("N"));
      db.Add(result);
      var placeName = new PlaceName()
      {
        Name = name
      };
      var form = (string)structure.Child("FORM");
      if (!string.IsNullOrWhiteSpace(form))
      {
        var formParts = form.Split(',').Select(p => p.Trim()).ToList();
        var nameParts = name.Split(',').Select(p => p.Trim()).ToList();
        if (nameParts.Count == formParts.Count)
        {
          for (var i = 0; i < nameParts.Count; i++)
          {
            if (!string.IsNullOrEmpty(formParts[i])
              && !string.IsNullOrEmpty(nameParts[i]))
            {
              placeName.Parts.Add(new KeyValuePair<string, string>(formParts[i], nameParts[i]));
            }
          }
        }
      }
      result.Names.Add(placeName);
      AddCommonFields(structure, result, db);
      return result;
    }

    private Organization Organization(string name, Database db)
    {
      if (_orgs.TryGetValue(name, out var result))
        return result;
      result = new Organization();
      _orgs.Add(name, result);
      result.Name = name;
      result.Id.Add(Guid.NewGuid().ToString("N"));
      db.Add(result);
      return result;
    }

    private Citation Citation(GStructure structure, Database db)
    {
      var result = new Citation
      {
        RecordNumber = (string)structure.Child("_APID")
      };
      result.Id.Add(Guid.NewGuid().ToString("N"));
      db.Add(result);

      var notes = new List<string>();
      if (_sources.TryGetValue(structure.Pointer, out var source))
      {
        result.Author = (string)source.Child("AUTH");
        result.Title = (string)source.Child("TITL");
        var publisher = (string)source.Child("PUBL");
        if (!string.IsNullOrEmpty(publisher))
          result.Publisher = Organization(publisher, db);
        var repository = (string)source.Child("REPO");
        if (!string.IsNullOrEmpty(repository))
          result.Repository = Organization(repository, db);
        var sourceNote = (string)source.Child("NOTE");
        if (!string.IsNullOrEmpty(sourceNote))
          notes.Add(sourceNote);
        if (source.Child("PUBL")?.Child("DATE") != null
          && source.Child("PUBL").Child("DATE").TryGetDateRange(out var pubDateRange))
          result.DatePublished = pubDateRange;
      }

      AddCommonFields(structure, result, db);
      result.Attributes.Remove("APID");
      if (structure.Child("DATA")?.Child("DATE") != null
          && structure.Child("DATA").Child("DATE").TryGetDateRange(out var dateRange))
      {
        result.DatePublished = dateRange;
      }
      var citeNote = (string)structure.Child("DATA")?.Child("NOTE");
      if (!string.IsNullOrEmpty(citeNote))
        notes.Add(citeNote);

      foreach (var note in notes)
      {
        if (result.Url == null
          && Uri.TryCreate(note, UriKind.Absolute, out var uri)
          && uri.Scheme.StartsWith("http"))
          result.Url = uri;
        else
          result.Notes.Add(new Note()
          {
            Text = note,
            MimeType = note.IndexOf("</") > 0 ? "text/html" : null
          });
      }

      var page = (string)structure.Child("PAGE");
      result.SetPages(page);

      if (_citations.Add(result))
        return result;
      return _citations.First(c => c.Equals(result));
    }

    private void AddCustomAttributes(GStructure structure, IHasAttributes attributes, string path)
    {
      foreach (var attr in structure.Children().Where(s => s.Tag.StartsWith("_")))
      {
        var key = string.IsNullOrEmpty(path) ? attr.Tag.TrimStart('_') : path + "." + attr.Tag.TrimStart('_');
        var value = (string)attr;
        if (!string.IsNullOrWhiteSpace(value))
          attributes.Attributes[key] = value.Trim();
        if (attr.Children().Any())
          AddCustomAttributes(attr, attributes, key);
      }
    }

    private void AddCommonFields(GStructure structure, object primaryObject, Database db)
    {
      if (primaryObject is IHasAttributes attributes)
        AddCustomAttributes(structure, attributes, "");
      
      if (primaryObject is IHasCitations citations)
      {
        citations.Citations.AddRange(structure
          .Children("SOUR")
          .Select(s => Citation(s, db)));
      }

      if (primaryObject is IHasId ids)
      {
        
      }

      if (primaryObject is IHasLinks hasLinks)
      {
        
      }

      if (primaryObject is IHasMedia mediaContainer)
      {
        foreach (var objId in structure
          .Children("OBJE")
          .Select(s => s.Pointer))
        {
          if (db.TryGetValue(objId, out Media media))
            mediaContainer.Media.Add(media);
        }
      }

      if (primaryObject is IHasNotes notes)
      {
        
      }
    }

    private Media Media(GStructure structure, Database db)
    {
      var media = new Media()
      {
        Src = (string)structure.Child("FILE"),
      };
      media.Id.Add(structure.Id);
      media.Place = Place(structure.Child("PLAC"), db);
      if (structure.Child("DATE") != null
        && structure.Child("DATE").TryGetDateRange(out var dateRange))
        media.Date = dateRange;
      if (structure.Child("FILE") != null)
        media.Description = (string)structure.Child("FILE").Child("TITL");

      var rin = (string)structure.Child("RIN");
      if (!string.IsNullOrEmpty(rin))
        media.Attributes.Add("RIN", rin);
      AddCommonFields(structure, media, db);

      if (media.Attributes.TryGetValue("META", out var meta))
      {
        try
        {
          var metaXml = XElement.Parse(meta);
          if (metaXml.Name.LocalName == "metadataxml")
          {
            media.Attributes.Remove("META");
            foreach (var child in metaXml.Elements())
            {
              if (child.Name.LocalName == "content")
              {
                var note = new Note()
                {
                  Text = string.Join("\r\n", child.Elements("line")
                    .Select(e => (string)e)).Trim()
                };
                if (note.Text.IndexOf("</") > 0)
                  note.MimeType = "text/html";
                media.Notes.Add(note);
              }
              else if (child.Name.LocalName == "personas"
                && !string.IsNullOrEmpty((string)child))
              {
                media.Attributes.Add(child.Name.LocalName, (string)child);
              }
            }
          }
        }
        catch (XmlException) { }
      }

      if (media.Attributes.TryGetValue("ORIG.URL", out var origUrl)
        && Uri.TryCreate(origUrl, UriKind.Absolute, out var uri)
        && uri.Scheme.StartsWith("http"))
      {
        media.Attributes.Remove("ORIG.URL");
        media.Links.Add(new Link()
        {
          Url = uri
        });
      }
      return media;
    }

    private static bool TryGetEventType(string tag, out EventType eventType)
    {
      switch (tag)
      {
        case "ADOP": eventType = EventType.Adoption; return true;
        case "BAPM": eventType = EventType.Baptism; return true;
        case "BARM": eventType = EventType.BarMitzvah; return true;
        case "BASM": eventType = EventType.BatMitzvah; return true;
        case "BIRT": eventType = EventType.Birth; return true;
        case "BLES": eventType = EventType.Blessing; return true;
        case "BURI": eventType = EventType.Burial; return true;
        case "CENS": eventType = EventType.Census; return true;
        case "CHR": eventType = EventType.Christening; return true;
        case "CHRA": eventType = EventType.AdultChristening; return true;
        case "CONF": eventType = EventType.Confirmation; return true;
        case "CREM": eventType = EventType.Cremation; return true;
        case "DEAT": eventType = EventType.Death; return true;
        case "EMIG": eventType = EventType.Emigration; return true;
        case "FCOM": eventType = EventType.FirstCommunion; return true;
        case "GRAD": eventType = EventType.Graduation; return true;
        case "IMMI": eventType = EventType.Immigration; return true;
        case "NATU": eventType = EventType.Naturalization; return true;
        case "ORDN": eventType = EventType.Ordination; return true;
        case "PROB": eventType = EventType.Probate; return true;
        case "RETI": eventType = EventType.Retirement; return true;
        case "WILL": eventType = EventType.Will; return true;
        case "ANUL": eventType = EventType.Annulment; return true;
        case "DIV": eventType = EventType.Divorce; return true;
        case "DIVF": eventType = EventType.DivorceFiled; return true;
        case "ENGA": eventType = EventType.Engagement; return true;
        case "MARB": eventType = EventType.MarriageBann; return true;
        case "MARC": eventType = EventType.MarriageContract; return true;
        case "MARL": eventType = EventType.MarriageLicense; return true;
        case "MARR": eventType = EventType.Marriage; return true;
        case "MARS": eventType = EventType.MarriageSettlement; return true;
        case "RESI": eventType = EventType.Residence; return true;
        case "_MILT": eventType = EventType.MilitaryService; return true;
        case "EVEN": eventType = EventType.Generic; return true;
      }
      eventType = EventType.Generic;
      return false;
    }

    private FamilyLinkType GetRelationshipType(string relationship)
    {
      switch (relationship?.ToUpperInvariant() ?? "")
      {
        case "FATH":
        case "FATHER":
        case "HUSB":
        case "HUSBAND":
          return FamilyLinkType.Father;
        case "MOTH":
        case "MOTHER":
        case "WIFE":
          return FamilyLinkType.Mother;
        case "CHIL":
        case "CHILD":
          return FamilyLinkType.Child;
        case "GODP":
        case "GODPARENT":
          return FamilyLinkType.Godparent;
        case "NGHBR":
        case "NEIGHBOR":
          return FamilyLinkType.Neighbor;
        case "PARENT":
        case "SPOU":
        case "SPOUSE":
          return FamilyLinkType.Parent;
      }
      return FamilyLinkType.Other;
    }


    //public override string Tag
    //{
    //    get
    //    {
    //        switch (Type)
    //        {
    //            case EventType.Adoption: return "ADOP";
    //            case EventType.Baptism: return "BAPM";
    //            case EventType.BarMitzvah: return "BARM";
    //            case EventType.BasMitzvah: return "BASM";
    //            case EventType.Birth: return "BIRT";
    //            case EventType.Blessing: return "BLES";
    //            case EventType.Burial: return "BURI";
    //            case EventType.Census: return "CENS";
    //            case EventType.Christening: return "CHR";
    //            case EventType.AdultChristening: return "CHRA";
    //            case EventType.Confirmation: return "CONF";
    //            case EventType.Cremation: return "CREM";
    //            case EventType.Death: return "DEAT";
    //            case EventType.Emigration: return "EMIG";
    //            case EventType.FirstCommunion: return "FCOM";
    //            case EventType.Graduation: return "GRAD";
    //            case EventType.Immigration: return "IMMI";
    //            case EventType.Naturalization: return "NATU";
    //            case EventType.Ordination: return "ORDN";
    //            case EventType.Probate: return "PROB";
    //            case EventType.Retirement: return "RETI";
    //            case EventType.Will: return "WILL";
    //            case EventType.Annulment: return "ANUL";
    //            case EventType.Divorce: return "DIV";
    //            case EventType.DivorceFiled: return "DIVF";
    //            case EventType.Engagement: return "ENGA";
    //            case EventType.MarriageBann: return "MARB";
    //            case EventType.MarriageContract: return "MARC";
    //            case EventType.MarriageLicense: return "MARL";
    //            case EventType.Marriage: return "MARR";
    //            case EventType.MarriageSettlement: return "MARS";
    //        }
    //        return null;
    //    }
    //}
  }
}
