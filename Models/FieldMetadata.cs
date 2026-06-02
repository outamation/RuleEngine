namespace DemoRuleEngine.Models;

public class FieldMetadata
{
    public string FieldName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DataType { get; set; } = "Decimal"; // Decimal, String, Boolean, Date
    public string? ExampleValue { get; set; }
}
