using System.Xml.Linq;
using Sgml;

namespace FinanceDB.Cli.Parsing;

public static class OfxReader
{
    public static XDocument Parse(string rawContent)
    {
        var sgmlBody = StripHeader(rawContent);

        Console.WriteLine("=== SGML Body (first 500 chars) ===");
        Console.WriteLine(sgmlBody[..Math.Min(500, sgmlBody.Length)]);
        Console.WriteLine("===================================");

        var doc = NormalizeToXml(sgmlBody);

        Console.WriteLine("=== Normalized XML ===");
        Console.WriteLine(doc.ToString()[..Math.Min(1500, doc.ToString().Length)]);
        Console.WriteLine("======================");

        return doc;
    }

    private static string StripHeader(string content)
    {
        var ofxStart = content.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);

        if (ofxStart < 0)
        {
            throw new FormatException(
                "Invalid OFX content: could not locate <OFX> tag. " +
                "Ensure the input is a valid OFX 1.x or QFX file.");
        }

        return content[ofxStart..];
    }

    private static XDocument NormalizeToXml(string sgmlBody)
    {
        using var stringReader = new StringReader(sgmlBody);
        using var sgmlReader = new SgmlReader();
        sgmlReader.InputStream = stringReader;
        sgmlReader.WhitespaceHandling = System.Xml.WhitespaceHandling.Significant;
        sgmlReader.CaseFolding = CaseFolding.ToUpper;

        return XDocument.Load(sgmlReader);
    }
}
