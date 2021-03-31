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
        private const string XhtmlHeader = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.1//EN\" \"http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd\">\n";
        private const string InputFile = "spell_full - Updated 20Nov2016.csv";

        private static readonly ISet<char> BadChars = new HashSet<char>(Path.GetInvalidPathChars())
        {
            ' ', ',', '\'', '/'
        };

        public static void Main(string[] args)
        {
            //var interestingClasses = new HashSet<string>() { "Cleric", "Sorcerer", "Wizard", "Bard", "Druid" };
            var interestingClasses = new HashSet<string>() { "Cleric" };
            var lines = File.ReadAllLines(InputFile);
            var csvEntries = lines.Select(CsvEntry.Parse).ToList();
            var spells = csvEntries
                .Skip(1)
                .Select(Spell.FromCsvEntry)
                //.Where(s => s.GetSpellLevels().Any(kv => interestingClasses.Contains(kv.Key)))
                .OrderBy(s => s.Name)
                //.Take(10)
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

            using (var archive = new ZipArchive(File.Open(Path.Combine(dir.FullName, "book.epub"), FileMode.Create), ZipArchiveMode.Create))
            {
                GenerateBoilerplate(archive, spells);
                GenerateSpells(archive, spells);
            }
        }

        private static void GenerateSpells(ZipArchive archive, List<Spell> spells)
        {
            const string bookId = "PathfinderSpellEBook";

            const string opfNS = "http://www.idpf.org/2007/opf";
            const string dcNS = "http://purl.org/dc/elements/1.1/";

            XmlElement manifest;
            XmlElement spine;
            {
                var contentDoc = new XmlDocument();
                contentDoc.AppendChild(contentDoc.CreateXmlDeclaration("1.0", "UTF-8", null));
                contentDoc.AppendChild(contentDoc.CreateElement("package", opfNS));
                var opfRoot = contentDoc.DocumentElement;
                opfRoot.SetAttribute("unique-identifier", bookId);
                opfRoot.SetAttribute("version", "2.0");
                var metaData = contentDoc.CreateElement("metadata", opfNS);
                opfRoot.AppendChild(metaData);
                metaData.SetAttribute("xmlns:dc", dcNS);
                metaData.SetAttribute("xmlns:opf", opfNS);

                var title = contentDoc.CreateElement("dc", "title", dcNS);
                metaData.AppendChild(title);
                title.AppendChild(contentDoc.CreateTextNode(bookId));
                
                var creator = contentDoc.CreateElement("dc", "creator", dcNS);
                metaData.AppendChild(creator);
                creator.SetAttribute("role", opfNS, "aut");
                creator.AppendChild(contentDoc.CreateTextNode("Matthijs Hofstra"));

                var language = contentDoc.CreateElement("dc", "language", dcNS);
                metaData.AppendChild(language);
                language.AppendChild(contentDoc.CreateTextNode("en-US"));

                var rights = contentDoc.CreateElement("dc", "rights", dcNS);
                metaData.AppendChild(rights);
                rights.AppendChild(contentDoc.CreateTextNode("Public Domain"));

                var publisher = contentDoc.CreateElement("dc", "publisher", dcNS);
                metaData.AppendChild(publisher);
                publisher.AppendChild(contentDoc.CreateTextNode("Nobody"));

                var identifier = contentDoc.CreateElement("dc", "identifier", dcNS);
                metaData.AppendChild(identifier);
                identifier.SetAttribute("id", bookId);
                identifier.SetAttribute("scheme", opfNS, "UUID");
                identifier.AppendChild(contentDoc.CreateTextNode(bookId));

                manifest = contentDoc.CreateElement("manifest", opfNS);
                opfRoot.AppendChild(manifest);

                var ncx = contentDoc.CreateElement("item", opfNS);
                manifest.AppendChild(ncx);
                ncx.SetAttribute("id", "toc.ncx");
                ncx.SetAttribute("href", "toc.ncx");
                ncx.SetAttribute("media-type", "application/x-dtbncx+xml");

                var css = contentDoc.CreateElement("item", opfNS);
                manifest.AppendChild(css);
                css.SetAttribute("id", "style");
                css.SetAttribute("href", "Styles/Style.css");
                css.SetAttribute("media-type", "text/css");

                spine = contentDoc.CreateElement("spine", opfNS);
                opfRoot.AppendChild(spine);
                //var itemRef = contentDoc.CreateElement("itemref", opfNS);
                //spine.AppendChild(itemRef);
                //itemRef.SetAttribute("idref", "toc");
                spine.SetAttribute("toc", "toc.ncx");
            }

            const string ncxNS = "http://www.daisy.org/z3986/2005/ncx/";
            XmlElement navMap;
            {
                
                var tocDoc = new XmlDocument();
                tocDoc.AppendChild(tocDoc.CreateXmlDeclaration("1.0", "UTF-8", null));
                tocDoc.AppendChild(tocDoc.CreateElement("ncx", ncxNS));
                tocDoc.DocumentElement.SetAttribute("version", "2005-1");

                var head = tocDoc.CreateElement("head", ncxNS);
                tocDoc.DocumentElement.AppendChild(head);
                
                var metaData = new Dictionary<string, string>
                {
                    { "dtd:uid", bookId },
                    { "dtd:depth", "1"},
                    { "dtd:totalPageCount", "0" },
                    { "dtd:maxPageNumber", "0" }
                };

                foreach (var kv in metaData)
                {
                    var meta = tocDoc.CreateElement("meta", ncxNS);
                    head.AppendChild(meta);
                    meta.SetAttribute("name", kv.Key);
                    meta.SetAttribute("content", kv.Value);
                }

                var title = tocDoc.CreateElement("docTitle", ncxNS);
                tocDoc.DocumentElement.AppendChild(title);
                var text = tocDoc.CreateElement("text", ncxNS);
                title.AppendChild(text);
                text.AppendChild(tocDoc.CreateTextNode(bookId));

                navMap = tocDoc.CreateElement("navMap", ncxNS);
                tocDoc.DocumentElement.AppendChild(navMap);
            }

            var classes = spells.SelectMany(s => s.GetSpellLevels().Select(kv => kv.Key)).Distinct().ToList();
            foreach (var spell in spells)
            {
                var entry = archive.CreateEntry($"OEBPS/{CreateEntryName(spell.Name)}.xhtml");
                using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
                {
                    writer.Write(XhtmlHeader);
                    writer.Write(ToXhtml(spell, classes).OuterXml);
                }

                var item = manifest.OwnerDocument.CreateElement("item", opfNS);
                manifest.AppendChild(item);
                item.SetAttribute("id", entry.Name);
                item.SetAttribute("href", entry.Name);
                item.SetAttribute("media-type", "application/xhtml+xml");
                var itemRef = spine.OwnerDocument.CreateElement("itemref", opfNS);
                spine.AppendChild(itemRef);
                itemRef.SetAttribute("idref", entry.Name);
            }

            var classToSpells = classes.ToDictionary(c => c, c => new List<Spell>(), StringComparer.OrdinalIgnoreCase);
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

            var manifestFragment = manifest.OwnerDocument.CreateDocumentFragment();
            var spineFragment = spine.OwnerDocument.CreateDocumentFragment();
            foreach (var kv in classToSpells.OrderBy(kv => kv.Key))
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
                css.SetAttribute("href", "Styles/Style.css");
                html.FirstChild.AppendChild(css);
                var body = html.LastChild;
                body.AppendChild(doc.CreateElement("h1")).AppendChild(doc.CreateTextNode(kv.Key));

                var interestingSpells = kv.Value
                    .GroupBy(s => s.GetSpellLevels().Find(sl => String.Equals(kv.Key, sl.Key, StringComparison.OrdinalIgnoreCase)).Value)
                    .OrderBy(g => g.Key);
                foreach (var grouping in interestingSpells)
                {
                    var header = doc.CreateElement("h2");
                    header.AppendChild(doc.CreateTextNode($"Level {grouping.Key} spells"));
                    body.AppendChild(header);
                    //var table = doc.CreateElement("table");
                    //body.AppendChild(table);
                    foreach (var entry in grouping.OrderBy(s => s.Name))
                    {
                        //var row = doc.CreateElement("tr");
                        //table.AppendChild(row);
                        //var col = doc.CreateElement("td");
                        //row.AppendChild(col);

                        var link = doc.CreateElement("a");
                        link.AppendChild(doc.CreateTextNode(entry.Name));
                        link.SetAttribute("href", $"{CreateEntryName(entry.Name)}.xhtml");

                        var par = doc.CreateElement("p");
                        body.AppendChild(par);
                        par.AppendChild(link);
                        if (!string.IsNullOrWhiteSpace(entry.Summary))
                        {
                            par.AppendChild(doc.CreateElement("br"));
                            par.AppendChild(doc.CreateTextNode(entry.Summary));
                        }
                        /*col.AppendChild(link);
                        col = doc.CreateElement("td");
                        row.AppendChild(col);
                        col.AppendChild(doc.CreateTextNode(entry.Summary));*/
                    }
                }

                var zipEntry = archive.CreateEntry($"OEBPS/{CreateEntryName(kv.Key)}.xhtml");
                using (var writer = new StreamWriter(zipEntry.Open(), Encoding.UTF8))
                {
                    writer.Write(XhtmlHeader);
                    doc.ToHtml(writer, new AngleSharp.XHtml.XhtmlMarkupFormatter());
                }

                var item = manifestFragment.OwnerDocument.CreateElement("item", opfNS);
                manifestFragment.AppendChild(item);
                item.SetAttribute("id", kv.Key);
                item.SetAttribute("href", zipEntry.Name);
                item.SetAttribute("media-type", "application/xhtml+xml");
                var itemRef = spineFragment.OwnerDocument.CreateElement("itemref", opfNS);
                spineFragment.AppendChild(itemRef);
                itemRef.SetAttribute("idref", kv.Key);

                var navPoint = navMap.OwnerDocument.CreateElement("navPoint", ncxNS);
                navMap.AppendChild(navPoint);
                navPoint.SetAttribute("id", kv.Key);
                navPoint.SetAttribute("playOrder", $"{navMap.ChildNodes.Count}");
                var navLabel = navMap.OwnerDocument.CreateElement("navLabel", ncxNS);
                navPoint.AppendChild(navLabel);
                navLabel.AppendChild(navMap.OwnerDocument.CreateElement("text", ncxNS)).AppendChild(navMap.OwnerDocument.CreateTextNode(kv.Key));
                var content = navMap.OwnerDocument.CreateElement("content", ncxNS);
                navPoint.AppendChild(content);
                content.SetAttribute("src", $"{CreateEntryName(kv.Key)}.xhtml");
            }

            manifest.AppendChild(manifestFragment);
            spine.InsertBefore(spineFragment, spine.FirstChild);


            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "  ",
                CloseOutput = true
            };
            var manifestEntry = archive.CreateEntry("OEBPS/content.opf");
            using (var writer = XmlWriter.Create(manifestEntry.Open(), settings))
            {
                manifest.OwnerDocument.WriteTo(writer);
            }

            var ncxEntry = archive.CreateEntry("OEBPS/toc.ncx");
            using (var writer = XmlWriter.Create(ncxEntry.Open(), settings))
            {
                navMap.OwnerDocument.WriteTo(writer);
            }
        }

        private static string CreateEntryName(string name)
        {
            var fileNameChars = name.ToCharArray();
            for (var i = 0; i < fileNameChars.Length; i++)
            {
                if (BadChars.Contains(fileNameChars[i]))
                {
                    fileNameChars[i] = '_';
                }
            }

            return new string(fileNameChars);
        }

        private static void GenerateBoilerplate(ZipArchive archive, List<Spell> spells)
        {
            GenerateMimeType(archive);
            GenerateContainerXml(archive);
            GenerateCss(archive);
        }

        private static void GenerateCss(ZipArchive archive) {
            var entry = archive.CreateEntry("OEBPS/Styles/Style.css");
            using (var output = entry.Open())
            using (var input = File.OpenRead("Styles/Style.css"))
            {
                input.CopyTo(output);
            }
        }

        private static void GenerateMimeType(ZipArchive archive)
        {
            const string content = "application/epub+zip";
            var entry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var mimeType = new StreamWriter(entry.Open(), Encoding.ASCII))
            {
                mimeType.Write(content);
            }
        }

        private static void GenerateContainerXml(ZipArchive archive) {
            var container = new XmlDocument();
            var ns = "urn:oasis:names:tc:opendocument:xmlns:container";
            var root = container.CreateElement("container", ns);
            container.AppendChild(root);
            root.SetAttribute("version", "1.0");
            var rootFiles = container.CreateElement("rootfiles", ns);
            root.AppendChild(rootFiles);
            var rootFile = container.CreateElement("rootfile", ns);
            rootFiles.AppendChild(rootFile);
            rootFile.SetAttribute("full-path", "OEBPS/content.opf");
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
            style.SetAttribute("href", "Styles/Style.css");
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
