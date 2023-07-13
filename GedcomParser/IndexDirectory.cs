using GedcomParser.Model;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace GedcomParser
{
  internal class IndexDirectory
  {
    public static void ProcessDirectory(string directory, string target, string pathPrefix = null)
    {
      var mediaList = new List<Media>();
      foreach (var file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
      {
        switch (Path.GetExtension(file).ToUpperInvariant())
        {
          case ".JPG":
          case ".JPEG":
          case ".CR2":
            var relative = Path.ChangeExtension(file.Substring(directory.Length).Replace('\\', '/').TrimStart('/'), ".jpg");
            var img = default(MagickImage);
            try
            {
              img = new MagickImage(file, MagickFormat.Jpeg);
              File.Move(file, Path.Combine(target, relative), true);
            }
            catch (Exception)
            {
              img = new MagickImage(file);
              img.Write(Path.Combine(target, relative));
            }
            
            if (!string.IsNullOrEmpty(pathPrefix))
              relative = pathPrefix + "/" + relative;

            var media = new Media
            {
              Src = relative,
              Width = img.Width,
              Height = img.Height
            };
            var exif = img.GetExifProfile();
            if (exif != null)
            {
              var date = exif.GetValue(ExifTag.DateTime);
              if (!string.IsNullOrEmpty(date?.Value)
                && DateTime.TryParseExact(date.Value, "yyyy':'MM':'dd HH':'mm':'ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dateTime))
              {
                media.Date = ExtendedDateRange.Parse(dateTime.ToString("s"));
              }
            }
            mediaList.Add(media);
            break;
        }
      }

      using (var writer = new StreamWriter(Path.Combine(target, "index.yaml")))
      {
        var yamlWriter = new YamlWriter();
        var mapping = new YamlMappingNode()
        {
          { "media", new YamlSequenceNode(mediaList.Select(m => yamlWriter.Media(m))) }
        };

        new YamlStream(new YamlDocument(mapping)).Save(writer, false);
      }
    }

    /*using var image = new MagickImage(SampleFiles.StillLifeCR2);
image.Write("StillLife.jpg");*/
  }
}
