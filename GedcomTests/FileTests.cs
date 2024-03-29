﻿using GedcomParser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace GedcomTests
{
  [TestClass]
    public class FileTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestParse()
        {
            var dirParts = TestContext.TestDeploymentDir.Replace('\\', '/').Split('/');
            var baseDir = string.Join("/", dirParts, 0, Array.IndexOf(dirParts, "bin"));
            var filePath = Path.Combine(baseDir, "Files", "TGC551.ged");
            var structure = GStructure.Load(filePath);

            Assert.AreEqual(@"Submitter address line 1
Submitter address line 2
Submitter address line 3
Submitter address line 4", (string)structure.Child("SUBM").Child("ADDR"));
            Assert.AreEqual("1998-01-01T13:57:24", ((DateTime)structure.Child("HEAD").Child("DATE")).ToString("s"));
            Assert.AreEqual("Test link to a graphics file about the main Submitter of this file.", (string)structure.Child("SUBM").Child("OBJE").Child("NOTE"));
            Assert.AreEqual("Charlie Accented ANSEL", (string)structure.Child("INDI").Child("NAME"));
            Assert.AreEqual("ANSEL", ((PersonName)structure.Child("INDI").Child("NAME")).Surname);
            Assert.AreEqual(@"This file demonstrates all tags that are allowed in GEDCOM 5.5. Here are some comments about the HEADER record and comments about where to look for information on the other 9 types of GEDCOM records. Most other records will have their own notes that describe what to look for in that record and what to hope the importing software will find.

Many applications will fail to import these notes. The notes are therefore also provided with the files as a plain-text ""Read-Me"" file.

--------------------------
The HEADER Record:
     This record has all possible tags for a HEADER record. In uses one custom tag (""_HME"") to see what the software will say about custom tags.

--------------------------
INDIVIDUAL Records:
     This file has a small number of INDIVIDUAL records. The record named ""Joseph Tag Torture"" has all possible tags for an INDIVIDUAL record. All remaining  individuals have less tags. Some test specific features; for example:

     Name: Standard GEDCOM Filelinks
     Name: Nonstandard Multimedia Filelinks
     Name: General Custom Filelinks
     Name: Extra URL Filelinks
          These records link to multimedia files mentioned by the GEDCOM standard and to a variety of other types of multimedia files, general files, or URL names.

     Name: Chris Locked Torture
          Has a ""locked"" restriction (RESN) tag - should not be able to edit this record it. This record has one set of notes that is used to test line breaking in notes and a few other text-parsing features of the GEDCOM software. Read those notes to see what they are testing.

     Name: Sandy Privacy Torture
          Has a ""privacy"" restriction (RESN) tag. Is the tag recognized and how is the record displayed and/or printed?

     Name: Chris Locked Torture
     Name: Sandy Privacy Torture
     Name: Pat Smith Torture
          The three children in this file have unknown sex (no SEX tag). An ancestor tree from each should give five generations of ancestors.

     Name: Charlie Accented ANSEL
     Name: Lucy Special ANSEL
          The notes in these records use all possible special characters in the ANSEL character set. The header of this file denotes this file as using the ANSEL character set. The importing software should handle these special characters in a reasonable way.

     Name: Torture GEDCOM Matriarch
           All individuals in this file are related and all are descendants (or spouses of descendants) of Torture GEDCOM Matriarch. A descendant tree or report from this individual should show five generations of descendants.

--------------------------
FAMILY Records:
     The FAMILY record for ""Joseph Tag Torture"" (husband) and ""Mary First Jones"" (wife) has all tags allowed in family records. All other family records use only a few tags and are used to provide records for extra family links in other records.

--------------------------
SOURCE Records:
     There are two SOURCE records in this file. The ""Everything You Every Wanted to Know about GEDCOM Tags"" source has all possible GEDCOM tags for a SOURCE record. The other source only has only a few tags.

--------------------------
REPOSITORY Record:
     There is just one REPOSITORY record and it uses all possible tags for such a record.

--------------------------
SUBMITTER Records:
     This file has three SUBMITTER records. The ""John A. Nairn"" record has all tags allowed in such records. The second and third submitter are to test how programs input files with multiple submitters. The GEDCOM standard does not allow for notes in SUBMITTER records. Look in the ""Main Submitter"" to verify all address data comes through, that all three phone numbers appear, and that the multimedia file link is preserved.

--------------------------
MULTIMEDIA OBJECT Record:
     The one MULTIMEDIA record has all possible tags and even has encoded data for a small image of a flower. There are no known GEDCOM programs that can read or write such records. The record is included here to test how programs might respond to finding multimedia records present. There are possible plans to eliminate encoded multimedia objects in the next version of GEDCOM. In the future all multimedia will be included by links to other files. To test current file links and extended file links, see the ""Filelinks"" family records described above.

--------------------------
SUBMISSION Record:
     The one (maximum allowed) SUBMISSION record in this file has all possible tags for such a record.

--------------------------
NOTE Records:
     This file has many NOTE records. These are all linked to other records.

--------------------------
TRLR Records:
     This file ends in the standard TRLR record.

--------------------------
ADDITIONAL NOTES
     This file was originally created by H. Eichmann at <h.eichmann@mbox.iqo.uni-hannover.de> and posted on the Internet.

(NOTE: email addresses are listed here with double ""at"" signs. A rule of GEDCOM parsing is that these should be converted to single ""at"" at signs, but not many programs follow that rule. In addition, that rule is not needed and may be abandoned in a future version of GEDCOM).

This original file was extensively modified by J. A. Nairn using GEDitCOM 2.9.4 (1999-2001) at <support@geditcom.com> and posted on the Internet at <http://www.geditcom.com>. Some changes included many more notes, the use or more tags, extensive testing of multimedia file links, and some notes to test all special ANSEL characters.

Feel free to copy and use this GEDCOM file for any  non-commercial purpose.

For selecting the allowed tags, the GEDCOM standard Release 5.5 (2 JAN 1996) was used. Copyright: The Church of Jesus Christ of Latter-Day Saints, <gedcom@gedcom.org>.

You can download the GEDCOM 5.5 specs from: <ftp.gedcom.com/pub/genealogy/gedcom>. You can read the GEDCOM 5.5 specs on the Internet at <http://homepages.rootsweb.com/~pmcbride/gedcom/55gctoc.htm>.", (string)structure.Child("HEAD").Child("NOTE"));
            ;
        }
    }
}
