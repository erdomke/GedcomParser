using GedcomParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GedcomParser
{
  public class GedcomLoader
  {
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
      if (type != null)
      {
        if (Enum.TryParse<NameType>((string)type ?? "Other", true, out var nameType))
        {
          result.Type = nameType;
          result.TypeString = (string)type.Child("PHRASE");
        }
        else
        {
          result.TypeString = (string)type;
          result.Type = NameType.Other;
        }
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
      result.Id.Add(Guid.NewGuid().ToString("N"));
      db.Add(result);
      result.Names.Add((string)structure);
      AddCommonFields(structure, result, db);
      return result;
    }

    public Citation Citation(GStructure structure, Database db)
    {
      var result = new Citation
      {
        RecordNumber = (string)structure.Child("_APID")
      };
      result.Id.Add((string)structure);
      db.Add(result);
      AddCommonFields(structure, result, db);
      if (structure.Child("DATA")?.Child("DATE") != null
          && structure.Child("DATA").Child("DATE").TryGetDateRange(out var dateRange))
      {
        result.DatePublished = dateRange;
      }
      var note = (string)structure.Child("NOTE")
        ?? (string)structure.Child("DATA")?.Child("NOTE");
      if (!string.IsNullOrEmpty(note))
        result.Notes.Add(new Note()
        {
          Text = note
        });

      var page = (string)structure.Child("PAGE");
      if (!string.IsNullOrEmpty(page))
        page += string.Join("", structure.Child("PAGE").Children("CONC").Select(s => (string)s));
      result.SetPages(page);
      return result;
    }

    private void AddCommonFields(GStructure structure, object primaryObject, Database db)
    {
      if (primaryObject is IHasAttributes attributes)
      {
        foreach (var attr in structure.Children().Where(s => s.Tag.StartsWith("_")))
        {
          var value = (string)attr;
          if (!string.IsNullOrEmpty(value))
          {
            value += string.Join("", attr.Children("CONC").Select(s => (string)s));
            attributes.Attributes[attr.Tag] = value;
          }
        }
      }

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
      var media = new Media
      {
        Src = (string)structure.Child("FILE"),
      };
      media.Id.Add(structure.Id);
      if (structure.Child("DATE") != null
        && structure.Child("DATE").TryGetDateRange(out var dateRange))
        media.Date = dateRange;
      if (!string.IsNullOrEmpty(media.Src))
        media.Description = (string)structure.Child("FILE").Child("TITL");
      AddCommonFields(structure, media, db);
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
