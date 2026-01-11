using System.Globalization;
using System.Xml.Linq;

namespace FinanceDB;

public class QfxParser
{
    public List<Record> Transactions { get; } = new();

    public void Parse(string content, string accountId)
    {
        var doc = OfxReader.Parse(content);

        ExtractTransactions(doc, accountId);
    }

    private void ExtractTransactions(XDocument doc, string accountId)
    {
        var transactionElements = doc.Descendants("STMTTRN");

        int counter = 0;

        foreach (var txn in transactionElements)
        {
            Console.WriteLine("-------------");
            Console.WriteLine($"Record #{counter}");

            var record = ParseTransaction(txn, accountId);

            if (record != null)
            {
                Transactions.Add(record);
            }

            counter++;
        }
    }

    private Record? ParseTransaction(XElement txn, string accountId)
    {
        Console.WriteLine($"  Raw XML: {txn}");
        Console.WriteLine($"  Children: {string.Join(", ", txn.Elements().Select(e => e.Name.LocalName))}");

        var dateStr = GetElementValue(txn, "DTPOSTED");
        var amountStr = GetElementValue(txn, "TRNAMT");
        var name = GetElementValue(txn, "NAME");

        Console.WriteLine($"  DTPOSTED: {dateStr ?? "(null)"}");
        Console.WriteLine($"  TRNAMT:   {amountStr ?? "(null)"}");
        Console.WriteLine($"  NAME:     {name ?? "(null)"}");

        if (string.IsNullOrEmpty(name))
        {
            name = GetElementValue(txn, "MEMO");
            Console.WriteLine($"  MEMO:     {name ?? "(null)"}");
        }

        if (string.IsNullOrEmpty(dateStr) || string.IsNullOrEmpty(amountStr))
        {
            Console.WriteLine("  [SKIPPED - missing required fields]");
            return null;
        }

        var date = ParseOfxDate(dateStr);
        var amount = decimal.Parse(amountStr, CultureInfo.InvariantCulture);

        Console.WriteLine($"  Parsed -> Date: {date:yyyy-MM-dd}, Amount: {amount:C}");

        var key = new RecordKey(accountId, date, 0);
        return new Record(key, name ?? "", amount);
    }


    private static string? GetElementValue(XElement parent, string elementName)
    {
        return parent.Element(elementName)?.Value.Trim();
    }

    private static DateTime ParseOfxDate(string ofxDate)
    {
        var dateStr = ofxDate.Split('[')[0];
        var format = dateStr.Length >= 14 ? "yyyyMMddHHmmss" : "yyyyMMdd";

        return DateTime.ParseExact(
            dateStr[..Math.Min(dateStr.Length, format.Length)],
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal);
    }
}
