using System.Collections.Generic;
using System.Text;

namespace Bonehilda;

// Unity can serialize a mod's record at the root but omit arrays of that
// record type inside another class. Assemble the envelope explicitly.
internal static class ActivitySnapshotJson
{
    internal static string Encode(string version, string utc, IEnumerable<string> records, bool pretty = false)
    {
        var gap = pretty ? "\n" : "";
        return "{" + gap + "\"version\":" + Quote(version) + "," + gap + "\"utc\":" + Quote(utc)
            + "," + gap + "\"maids\":[" + gap + string.Join("," + gap, records) + gap + "]" + gap + "}";
    }

    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c == '"' || c == '\\') result.Append('\\').Append(c);
            else if (c < ' ') result.Append("\\u").Append(((int)c).ToString("x4"));
            else result.Append(c);
        }
        return result.Append('"').ToString();
    }
}
