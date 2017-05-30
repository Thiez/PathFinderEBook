using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

using AngleSharp;

namespace SpellEbook
{
    public class Program
    {
        private const string XhtmlHeader = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">\n";
        private const string InputFile = "spell_full - Updated 20Nov2016.csv";

        public static void Main(string[] args)
        {
            //var interestingClasses = new HashSet<string>() { "Cleric", "Sorcerer", "Wizard", "Bard", "Druid" };
            var interestingClasses = new HashSet<string>() { "Cleric" };
            var lines = File.ReadAllLines(InputFile);
            var csvEntries = lines.Select(CsvEntry.Parse).ToList();
            var spells = csvEntries
                .Skip(1)
                .Select(Spell.FromCsvEntry)
                .Where(s => s.GetSpellLevels().Any(kv => interestingClasses.Contains(kv.Key) && kv.Value == 3))
                .OrderBy(s => s.Name)
                .Take(60)
                .ToList();

            var dir = new DirectoryInfo("book");
            try
            {
                dir.Create();
            }
            catch
            {
                // ignored
            }
            
            using (var archive = new ZipArchive(File.Open(Path.Combine(dir.FullName, "book.zip"), FileMode.Create), ZipArchiveMode.Create))
            {
                GenerateBoilerplate(archive, spells);
            }
            
            
            Directory.CreateDirectory(Path.Combine(dir.FullName, "OEBPS"));
            foreach (var spell in spells)
            {
                var xml = $"{XhtmlHeader}{ToXhtml(spell, interestingClasses).OuterXml}";
                var fileNameChars = (spell.Name + ".xhtml").ToCharArray();
                var badChars = Path.GetInvalidFileNameChars();
                for (var i = 0; i < fileNameChars.Length; i++)
                {
                    if (badChars.Contains(fileNameChars[i]))
                    {
                        fileNameChars[i] = '_';
                    }
                }

                File.WriteAllText(Path.Combine(dir.FullName, "OEBPS", new string(fileNameChars)), xml);
            }

            var classToSpells = interestingClasses.ToDictionary(c => c, c => new List<Spell>(), StringComparer.OrdinalIgnoreCase);
            foreach (var spell in spells)
            {
                foreach (var kv in spell.GetSpellLevels())
                {
                    List<Spell> appendTo;
                    if (classToSpells.TryGetValue(kv.Key, out appendTo))
                    {
                        appendTo.Add(spell);
                    }
                }
            }

            foreach (var kv in classToSpells)
            {
                var doc = new AngleSharp.Parser.Html.HtmlParser().Parse(string.Empty);
                var html = (AngleSharp.Dom.IElement)doc.FirstChild;
                html.SetAttribute("xmlns", "http://www.w3.org/1999/xhtml");
                var title = doc.CreateElement("title");
                title.AppendChild(doc.CreateTextNode($"{kv.Key} spell list"));
                html.FirstChild.AppendChild(title);
                var css = doc.CreateElement("link");
                css.SetAttribute("rel", "stylesheet");
                css.SetAttribute("type", "text/css");
                css.SetAttribute("href", "../Styles/Style.css");
                html.FirstChild.AppendChild(css);
                var body = html.LastChild;

                var interestingSpells = kv.Value
                    .GroupBy(s => s.GetSpellLevels().Find(sl => String.Equals(kv.Key, sl.Key, StringComparison.OrdinalIgnoreCase)).Value)
                    .OrderBy(g => g.Key);
                foreach (var grouping in interestingSpells)
                {
                    var header = doc.CreateElement("h1");
                    header.AppendChild(doc.CreateTextNode($"Level {grouping.Key} spells"));
                    body.AppendChild(header);
                    var table = doc.CreateElement("table");
                    body.AppendChild(table);
                    foreach (var entry in grouping.OrderBy(s => s.Name))
                    {
                        var row = doc.CreateElement("tr");
                        table.AppendChild(row);
                        var col = doc.CreateElement("td");
                        row.AppendChild(col);
                        var link = doc.CreateElement("a");
                        link.AppendChild(doc.CreateTextNode(entry.Name));
                        link.SetAttribute("href", $"{entry.Name}.xhtml");
                        col.AppendChild(link);
                        col = doc.CreateElement("td");
                        row.AppendChild(col);
                        col.AppendChild(doc.CreateTextNode(entry.Summary));
                    }
                }

                using (var stream = new StreamWriter(File.Open(Path.Combine(dir.FullName, "OEBPS", $"{kv.Key}.xhtml"), FileMode.Create)))
                {
                    stream.Write(XhtmlHeader);
                    doc.ToHtml(stream, new AngleSharp.XHtml.XhtmlMarkupFormatter());
                }
            }

            
        }

        private static void GenerateBoilerplate(ZipArchive archive, List<Spell> spells)
        {
            GenerateMimeType(archive);
            GenerateContainerXml(archive);
            GenerateCss(archive);
        }

        private static void GenerateCss(ZipArchive archive) {
            var entry = archive.CreateEntry("Styles/Style.css");
            using (var output = entry.Open())
            using (var input = File.OpenRead("content/Styles/Style.css"))
            {
                input.CopyTo(output);
            }
        }

        private static void GenerateMimeType(ZipArchive archive)
        {
            const string content = "application/epub+zip";
            var entry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var mimeType = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                mimeType.Write(content);
            }
        }

        private static void GenerateContainerXml(ZipArchive archive) {
            var container = new XmlDocument();
            var ns = "urn:oasis:names:tc:opendocument:xmlns:container";
            var root = container.CreateElement("container", ns);
            container.AppendChild(root);
            var rootFiles = container.CreateElement("rootfiles", ns);
            root.AppendChild(rootFiles);
            var rootFile = container.CreateElement("rootfile", ns);
            rootFiles.AppendChild(rootFile);
            rootFile.SetAttribute("full-path", "content.opf");
            rootFile.SetAttribute("media-type", "application/oebps-package+xml");

            var entry = archive.CreateEntry("META-INF/container.xml");
            using (var sink = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                sink.Write(XhtmlHeader);
                sink.Write(container.InnerXml);
            }
        }

        public static XmlDocument ToXhtml(Spell spell, IReadOnlyCollection<string> interestingClasses)
        {
            var doc = new XmlDocument();
            doc.PreserveWhitespace = false;

            var html = doc.CreateElement("html");
            html.SetAttribute("xmlns", "http://www.w3.org/1999/xhtml");
            doc.AppendChild(html);
            var head = doc.CreateElement("head");
            var title = doc.CreateElement("title");
            title.InnerText = spell.Name;
            var style = doc.CreateElement("link");
            style.SetAttribute("rel", "stylesheet");
            style.SetAttribute("type", "text/css");
            style.SetAttribute("href", "../Styles/Style.css");
            head.AppendChild(title);
            head.AppendChild(style);
            html.AppendChild(head);

            var body = doc.CreateElement("body");
            html.AppendChild(body);
            {
                var name = doc.CreateElement("h1");
                name.InnerText = spell.Name;
                body.AppendChild(name);
            }

            var school = doc.CreateElement("p");
            body.AppendChild(school);
            {
                {
                    var span = doc.CreateElement("span");
                    span.SetAttribute("class", "spell-title");
                    span.InnerText = "School";
                    school.AppendChild(span);
                }

                var schoolTxt = doc.CreateTextNode(spell.School);
                school.AppendChild(schoolTxt);
                if (!string.IsNullOrEmpty(spell.SubSchool))
                {
                    school.AppendChild(doc.CreateTextNode(" ("));
                    var span = doc.CreateElement("span");
                    span.SetAttribute("class", "subschool");
                    span.InnerText = spell.SubSchool;
                    school.AppendChild(span);
                    school.AppendChild(doc.CreateTextNode(")"));
                }
                if (!string.IsNullOrEmpty(spell.Descriptor))
                {
                    school.AppendChild(doc.CreateTextNode(" ["));
                    var span = doc.CreateElement("span");
                    span.SetAttribute("class", "descriptor");
                    span.InnerText = spell.Descriptor;
                    school.AppendChild(span);
                    school.AppendChild(doc.CreateTextNode("]"));
                }
            }

            var level = doc.CreateElement("p");
            body.AppendChild(level);
            {
                {
                    var span = doc.CreateElement("span");
                    span.SetAttribute("class", "spell-title");
                    span.InnerText = "Level";
                    level.AppendChild(span);
                }

                var first = true;
                foreach (var kv in spell.GetSpellLevels().Where(kv => interestingClasses.Contains(kv.Key)))
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        level.AppendChild(doc.CreateTextNode(", "));
                    }
                    var span = doc.CreateElement("a");
                    span.SetAttribute("href", $"{kv.Key}.xhtml");
                    span.InnerText = kv.Key;
                    level.AppendChild(span);
                    level.AppendChild(doc.CreateTextNode(" " + kv.Value));
                }
            }

            AppendLine(body, "Casting time", spell.CastingTime);
            AppendLine(body, "Components", spell.Components);
            AppendLine(body, "Range", spell.Range);
            AppendLine(body, "Area", spell.Area);
            AppendLine(body, "Duration", spell.Duration);
            AppendLine(body, "Saving throw", spell.SavingThrow);
            AppendLine(body, "Spell resistance", spell.SpellResist);

            body.AppendChild(doc.CreateElement("p"));
            {
                var p = doc.CreateElement("p");
                p.InnerText = "Description";
                body.AppendChild(p);
                var fragment = doc.CreateDocumentFragment();
                fragment.InnerXml = spell.FormattedDescription;
                body.AppendChild(fragment);
            }

            return doc;
        }

        private static void AppendLine(XmlElement parent, string propertyName, string propertyValue)
        {
            if (!string.IsNullOrWhiteSpace(propertyValue))
            {
                var child = parent.OwnerDocument.CreateElement("p");
                parent.AppendChild(child);
                var span = parent.OwnerDocument.CreateElement("span");
                span.SetAttribute("class", "spell-title");
                span.AppendChild(parent.OwnerDocument.CreateTextNode(propertyName));
                child.AppendChild(span);
                child.AppendChild(parent.OwnerDocument.CreateTextNode(propertyValue));
            }
        }
    }
}
