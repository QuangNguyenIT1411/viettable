using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

var path = args[0];
using var fs = File.OpenRead(path);
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();

var seen = new SortedSet<string>(StringComparer.Ordinal);
var size = md.GetHeapSize(HeapIndex.UserString);
for (int offset = 1; offset < size; offset++)
{
    try
    {
        var handle = MetadataTokens.UserStringHandle(offset);
        var s = md.GetUserString(handle);
        if (string.IsNullOrWhiteSpace(s)) continue;
        if (s.Contains("api/") || s.Contains("/api") || s.Contains("healthz") || s.Contains("readyz") || s.Contains("/customer") || s.Contains("/order") || s.Contains("/menu") || s.Contains("/branch") || s.Contains("stats"))
        {
            seen.Add(s);
        }
    }
    catch
    {
    }
}

foreach (var s in seen) Console.WriteLine(s);
