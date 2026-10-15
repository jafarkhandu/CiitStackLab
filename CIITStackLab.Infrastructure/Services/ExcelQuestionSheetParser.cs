using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace CIITStackLab.Infrastructure.Services;

/// <summary>
/// Reads the first worksheet of an .xlsx file without flattening cells into lines.
/// Cell text, including embedded line breaks, remains in its original column.
/// </summary>
internal static class ExcelQuestionSheetParser
{
    private const long MaximumXmlCharacters = 10 * 1024 * 1024;
    private const int MaximumRows = 101;

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    public static Task<IReadOnlyList<ExcelQuestionSheetRow>> ReadRowsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidDataException("The Excel workbook metadata is missing.");
        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidDataException("The Excel workbook relationships are missing.");

        var workbook = LoadXml(workbookEntry);
        var relationships = LoadXml(relationshipsEntry);
        var firstSheet = workbook.Root?
            .Element(SpreadsheetNamespace + "sheets")?
            .Elements(SpreadsheetNamespace + "sheet")
            .FirstOrDefault();

        if (firstSheet is null)
        {
            throw new InvalidDataException("The Excel workbook does not contain a worksheet.");
        }

        var relationshipId = (string?)firstSheet.Attribute(OfficeRelationshipNamespace + "id");
        var target = relationships.Root?
            .Elements(PackageRelationshipNamespace + "Relationship")
            .Where(item => string.Equals(
                (string?)item.Attribute("Id"),
                relationshipId,
                StringComparison.Ordinal))
            .Select(item => (string?)item.Attribute("Target"))
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidDataException("The first Excel worksheet could not be located.");
        }

        var worksheetPath = NormalizeWorksheetPath(target);
        var worksheetEntry = archive.GetEntry(worksheetPath)
            ?? throw new InvalidDataException("The first Excel worksheet data is missing.");

        var sharedStrings = new List<string>();
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry is not null)
        {
            var sharedStringDocument = LoadXml(sharedStringsEntry);
            foreach (var item in sharedStringDocument.Root?
                         .Elements(SpreadsheetNamespace + "si")
                     ?? Enumerable.Empty<XElement>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                sharedStrings.Add(string.Concat(
                    item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)));
            }
        }

        var worksheet = LoadXml(worksheetEntry);
        var sheetData = worksheet.Root?.Element(SpreadsheetNamespace + "sheetData")
            ?? throw new InvalidDataException("The first worksheet does not contain readable rows.");

        var rows = new List<ExcelQuestionSheetRow>();
        foreach (var rowElement in sheetData.Elements(SpreadsheetNamespace + "row"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowNumber = int.TryParse((string?)rowElement.Attribute("r"), out var parsedRowNumber)
                ? parsedRowNumber
                : rows.Count + 1;
            var cells = new Dictionary<int, string>();

            foreach (var cell in rowElement.Elements(SpreadsheetNamespace + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                var columnIndex = GetColumnIndex(reference);
                if (columnIndex < 0)
                {
                    continue;
                }

                var cellType = (string?)cell.Attribute("t");
                var value = cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;

                if (string.Equals(cellType, "s", StringComparison.Ordinal))
                {
                    if (!int.TryParse(value, out var sharedStringIndex)
                        || sharedStringIndex < 0
                        || sharedStringIndex >= sharedStrings.Count)
                    {
                        throw new InvalidDataException(
                            $"Worksheet row {rowNumber} contains an invalid shared-string reference.");
                    }

                    value = sharedStrings[sharedStringIndex];
                }
                else if (string.Equals(cellType, "inlineStr", StringComparison.Ordinal))
                {
                    value = string.Concat(
                        cell.Element(SpreadsheetNamespace + "is")?
                            .Descendants(SpreadsheetNamespace + "t")
                            .Select(text => text.Value)
                        ?? Enumerable.Empty<string>());
                }

                cells[columnIndex] = value;
            }

            if (cells.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                rows.Add(new ExcelQuestionSheetRow(rowNumber, cells));
                if (rows.Count > MaximumRows)
                {
                    throw new InvalidDataException(
                        "The spreadsheet contains too many populated rows. Excel question uploads are limited to 100 questions plus the header row.");
                }
            }
        }

        return Task.FromResult<IReadOnlyList<ExcelQuestionSheetRow>>(rows);
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        if (entry.Length > MaximumXmlCharacters)
        {
            throw new InvalidDataException("An Excel workbook XML part is too large to read safely.");
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumXmlCharacters
        };

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static string NormalizeWorksheetPath(string target)
    {
        var normalized = target.Replace('\\', '/').TrimStart('/');
        if (!normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "xl/" + normalized;
        }

        var segments = new List<string>();
        foreach (var segment in normalized.Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new InvalidDataException("The Excel worksheet path is invalid.");
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return string.Join('/', segments);
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return -1;
        }

        var index = 0;
        var hasColumn = false;
        foreach (var character in cellReference)
        {
            if (character is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
            {
                hasColumn = true;
                index = checked(index * 26 + char.ToUpperInvariant(character) - 'A' + 1);
            }
            else
            {
                break;
            }
        }

        return hasColumn ? index - 1 : -1;
    }
}

internal sealed record ExcelQuestionSheetRow(
    int RowNumber,
    IReadOnlyDictionary<int, string> Cells);
