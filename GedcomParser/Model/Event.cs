using System;
using System.Diagnostics;
using System.Linq;

namespace GedcomParser
{
    [DebuggerDisplay("{Tag,nq} {Date}")]
    public class Event
    {
        public EventType Type { get; set; }
        public ExtendedDateRange Date { get; set; }

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

        public Event() { }

        public Event(GStructure structure)
        {
            if (TryGetEventType(structure.Tag, out var type))
                Type = type;
            else
                throw new ArgumentException($"Structure {structure.Tag} does not represent a valid event.");
            if (structure.Child("DATE") != null
                && structure.Child("DATE").TryGetDateRange(out var dateRange))
                Date = dateRange;
            else
                Date = default;
        }

        public static bool TryGetEventType(string tag, out EventType eventType)
        {
            switch (tag)
            {
                case "ADOP": eventType = EventType.Adoption; return true;
                case "BAPM": eventType = EventType.Baptism; return true;
                case "BARM": eventType = EventType.BarMitzvah; return true;
                case "BASM": eventType = EventType.BasMitzvah; return true;
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
            }
            eventType = EventType.Generic;
            return false;
        }
    }
}
