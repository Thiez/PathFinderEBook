using System;
using System.Collections.Generic;
using System.Text;

namespace SpellEbook
{

    public class CsvEntry
    {

        public IReadOnlyList<string> Columns { get; set; }

        public static CsvEntry Parse(string line)
        {
            line = line.Trim();
            var columns = new List<string>();
            var from = 0;
            while (from < line.Length)
            {
                if (line[from] == '"' && from + 1 < line.Length && line[from + 1] != '"')
                {
                    from++;
                    var to = from;
                    for (; to < line.Length; to++)
                    {
                        if (line[to] == '"')
                        {
                            if (to + 1 < line.Length && line[to + 1] == '"')
                            {
                                to++;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    var chunk = line.Substring(from, to - from);
                    chunk = chunk.Trim();
                    chunk = chunk.Replace("\"\"", "\"");
                    columns.Add(chunk);
                    from = to + 2;
                }
                else
                {
                    var to = from;
                    for (; to < line.Length; to++)
                    {
                        if (line[to] == ',')
                        {
                            break;
                        }
                    }
                    var chunk = line.Substring(from, to - from);
                    chunk = chunk.Trim();
                    columns.Add(chunk);
                    from = to + 1;
                }
            }

            return new CsvEntry { Columns = columns };
        }

        public override string ToString()
        {
            return "CsvEntry {" + string.Join(" -- ", Columns) + "}";
        }
    }

}