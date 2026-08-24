namespace CoffeeNChill.Functions.Models;

public class StaffDocumentInfo
{
    public string FileName { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public DateTimeOffset LastModified { get; set; }
}