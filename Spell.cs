using System;
using System.Collections.Generic;

using AngleSharp.Dom;
using AngleSharp.Parser.Html;

namespace SpellEbook
{

    public class Spell
    {
        public string Name { get; set; }

        public string School { get; set; }

        public string SubSchool { get; set; }

        public string Descriptor { get; set; }

        public string Level { get; set; }

        public string CastingTime { get; set; }

        public string Components { get; set; }

        public string CostlyComponents { get; set; }

        public string Range { get; set; }

        public string Area { get; set; }

        public string Effect { get; set; }

        public string Targets { get; set; }

        public string Duration { get; set; }

        public bool Dismissable { get; set; }

        public bool Shapeable { get; set; }

        public string SavingThrow { get; set; }

        public string SpellResist { get; set; }

        public string Description { get; set; }

        public string Source { get; set; }

        public string FormattedDescription { get; set; }

        public bool Verbal { get; set; }

        public bool Somatic { get; set; }

        public bool Material { get; set; }

        public bool Focus { get; set; }

        public bool DivineFocus { get; set; }

        public int Cleric { get; set; }

        public int Sorcerer { get; set; }

        public int Wizard { get; set; }

        public int Druid { get; set; }
        public int Ranger { get; set; }
        public int Bard { get; set; }
        public int Paladin { get; set; }
        public int Alchemist { get; set; }
        public int Summoner { get; set; }
        public int Witch { get; set; }
        public int Inquisitor { get; set; }
        public int Oracle { get; set; }
        public int AntiPaladin { get; set; }
        public int Magus { get; set; }
        public int Adept { get; set; }

        public string Summary { get; set; }

        public List<KeyValuePair<string, int>> GetSpellLevels()
        {
            var result = new List<KeyValuePair<string, int>>();
            Action<string, int> add = (k, v) => result.Add(new KeyValuePair<string, int>(k, v));
            if (Adept >= 0)
            {
                add("Adept", Adept);
            }
            if (Alchemist >= 0)
            {
                add("Alchemist", Alchemist);
            }
            if (AntiPaladin >= 0)
            {
                add("AntiPaladin", AntiPaladin);
            }
            if (Bard >= 0)
            {
                add("Bard", Bard);
            }
            if (Cleric >= 0)
            {
                add("Cleric", Cleric);
            }
            if (Druid >= 0)
            {
                add("Druid", Druid);
            }
            if (Inquisitor >= 0)
            {
                add("Inquisitor", Inquisitor);
            }
            if (Magus >= 0)
            {
                add("Magus", Magus);
            }
            if (Oracle >= 0)
            {
                add("Oracle", Oracle);
            }
            if (Paladin >= 0)
            {
                add("Paladin", Paladin);
            }
            if (Ranger >= 0)
            {
                add("Ranger", Ranger);
            }
            if (Sorcerer >= 0)
            {
                add("Sorcerer", Sorcerer);
            }
            if (Summoner >= 0)
            {
                add("Summoner", Summoner);
            }
            if (Witch >= 0)
            {
                add("Witch", Witch);
            }
            if (Wizard >= 0)
            {
                add("Wizard", Wizard);
            }

            return result;
        }

        public static Spell FromCsvEntry(CsvEntry entry)
        {
            var result = new Spell();

            result.Name = entry.Columns[0];
            result.School = entry.Columns[1];
            result.SubSchool = entry.Columns[2];
            result.Descriptor = entry.Columns[3];
            result.Level = entry.Columns[4];
            result.CastingTime = entry.Columns[5];
            result.Components = entry.Columns[6];
            result.CostlyComponents = entry.Columns[7];
            result.Range = entry.Columns[8];
            result.Area = entry.Columns[9];
            result.Effect = entry.Columns[10];
            result.Targets = entry.Columns[11];
            result.Duration = entry.Columns[12];
            result.Dismissable = entry.Columns[13] == "1";
            result.Shapeable = entry.Columns[14] == "1";
            result.SavingThrow = entry.Columns[15];
            result.SpellResist = entry.Columns[16];
            result.Description = entry.Columns[17];
            result.FormattedDescription = FormatDescription(entry.Columns[18]);
            result.Source = entry.Columns[19];
            result.Verbal = entry.Columns[21] == "1";
            result.Somatic = entry.Columns[22] == "1";
            result.Material = entry.Columns[23] == "1";
            result.Focus = entry.Columns[24] == "1";
            result.DivineFocus = entry.Columns[25] == "1";
            result.Summary = entry.Columns[44];


            result.Sorcerer = TryParseSpellLevel(entry.Columns[26]);
            result.Wizard = TryParseSpellLevel(entry.Columns[27]);
            result.Cleric = TryParseSpellLevel(entry.Columns[28]);
            result.Druid = TryParseSpellLevel(entry.Columns[29]);
            result.Ranger = TryParseSpellLevel(entry.Columns[30]);
            result.Bard = TryParseSpellLevel(entry.Columns[31]);
            result.Paladin = TryParseSpellLevel(entry.Columns[32]);
            result.Alchemist = TryParseSpellLevel(entry.Columns[33]);
            result.Summoner = TryParseSpellLevel(entry.Columns[34]);
            result.Witch = TryParseSpellLevel(entry.Columns[35]);
            result.Inquisitor = TryParseSpellLevel(entry.Columns[36]);
            result.Oracle = TryParseSpellLevel(entry.Columns[37]);
            result.AntiPaladin = TryParseSpellLevel(entry.Columns[38]);
            result.Magus = TryParseSpellLevel(entry.Columns[39]);
            result.Adept = TryParseSpellLevel(entry.Columns[40]);

            return result;
        }

        private static int TryParseSpellLevel(string level)
        {
            int result;
            return int.TryParse(level, out result) ? result : -1;
        }

        private static string FormatDescription(string description)
        {
            Func<INode, INode> next = n =>
            {
                if (n.FirstChild != null)
                {
                    return n.FirstChild;
                }
                while (n != null && n.NextSibling == null)
                {
                    n = n.Parent;
                }

                return n != null
                    && !string.Equals(n.NodeName, "html", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(n.NodeName, "body", StringComparison.OrdinalIgnoreCase)
                    ? n.NextSibling
                    : null;
            };
            var parser = new HtmlParser();
            var body = parser.Parse(description).QuerySelector("html body");
            var current = body.FirstChild;
            while (current != null)
            {
                switch (current.NodeType)
                {
                    case NodeType.Element:
                        if (string.Equals(current.NodeName, "p", System.StringComparison.OrdinalIgnoreCase))
                        {
                            var elem = ((AngleSharp.Dom.Html.IHtmlElement)current);
                            while (elem.Attributes.Length > 0)
                            {
                                elem.RemoveAttribute(elem.Attributes[0].Name);
                            }

                            goto default;

                        }

                        INode nextCurrent;
                        if (current.HasChildNodes)
                        {
                            nextCurrent = current.FirstChild;
                            while (current.HasChildNodes)
                            {
                                var toMove = current.FirstChild;
                                current.RemoveChild(toMove);
                                current.Parent.InsertBefore(toMove, current);
                            }
                        }
                        else
                        {
                            nextCurrent = next(current);
                        }

                        current.Parent.RemoveChild(current);
                        current = nextCurrent;
                        break;
                    default:
                        current = next(current);
                        break;
                }
            }

            var writer = new System.IO.StringWriter();
            foreach (var child in body.Children)
            {
                child.ToHtml(writer, new AngleSharp.XHtml.XhtmlMarkupFormatter());
            }

            return writer.ToString();
        }
    }

}