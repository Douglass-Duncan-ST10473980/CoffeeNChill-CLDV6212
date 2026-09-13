namespace CoffeeNChill.Functions.DTO;


/**
 * Class Created By Douglass - ST10473980
 * Based on Neha ST10478910's Code
 */
public class UpdateMenuItemRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Price { get; set; }
    public bool? IsAvailable { get; set; }
}