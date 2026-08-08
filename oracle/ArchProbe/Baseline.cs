namespace ArchProbe;

/// <summary>
/// One type as recorded by an earlier run. The baseline format is just a previous
/// types.csv — the run's own output is its persistence format, so any archived run can
/// serve as a baseline and no separate database is needed.
/// </summary>
sealed class BaselineRow
{
    public string Id = "";
    public string Name = "";
    public string Kind = "";
    public string KindSpan = "";
    public int FanIn;
    public int FanOut;
    public int FanOutEffective;
    public int MaxMemberCyclomatic;
    public double GlobalFanInPctl;
    public bool HasKindSpan;
}

static class Baseline
{
    public static Dictionary<string, BaselineRow> Load(string path)
    {
        var rows = new Dictionary<string, BaselineRow>(StringComparer.Ordinal);
        using var reader = new StreamReader(path);

        var header = reader.ReadLine();
        if (header == null) return rows;

        var columns = ParseLine(header);
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++) index[columns[i]] = i;

        // KindSpan was added later than the other columns; an older baseline simply
        // doesn't get the layer-span drift message rather than failing to load.
        var hasKindSpan = index.ContainsKey("KindSpan");

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            var fields = ParseLine(line);

            string Get(string name) =>
                index.TryGetValue(name, out var i) && i < fields.Count ? fields[i] : "";

            int GetInt(string name) => int.TryParse(Get(name), out var v) ? v : 0;
            double GetDouble(string name) => double.TryParse(Get(name), out var v) ? v : 0;

            var id = Get("Id");
            if (string.IsNullOrEmpty(id)) continue;

            rows[id] = new BaselineRow
            {
                Id = id,
                Name = Get("Name"),
                Kind = Get("Kind"),
                KindSpan = Get("KindSpan"),
                HasKindSpan = hasKindSpan,
                FanIn = GetInt("FanIn"),
                FanOut = GetInt("FanOut"),
                FanOutEffective = GetInt("FanOutEffective"),
                MaxMemberCyclomatic = GetInt("MaxMemberCyclomatic"),
                GlobalFanInPctl = GetDouble("GlobalFanInPctl"),
            };
        }

        return rows;
    }

    /// <summary>Minimal RFC4180 field splitter — enough for what WriteTypesCsv emits.</summary>
    static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }

        fields.Add(current.ToString());
        return fields;
    }
}
