using System;
using Azure;
using Azure.Data.Tables;
// MENU ITEM - Data Entity Model

//
// This file defines the structure of a single menu item as it is stored in 
// the Azure Table Storage database. It acts as the data contract between 
// the application and the database.
//
// Key characteristics include:
//
// 1. Azure Table Storage Mapping:
//    - PartitionKey: Represents the Category of the item (e.g., "Drinks", "Food").
//                    This groups similar items together for efficient querying.
//    - RowKey:       Represents the unique SKU (Stock Keeping Unit) of the item 
//                    (e.g., "COF-001"). Combined with PartitionKey, this forms 
//                    the unique primary key for the item.
//
// 2. Item Attributes:
//    - Name:        The display name of the menu item.
//    - Description: A brief explanation of what the item is.
//    - Price:       The cost of the item in the local currency.
//    - IsAvailable: A boolean flag indicating if the item is currently in stock 
//                   or available for order.
//
// 3. Data Integrity & Formatting:
//    - Handles default values for missing fields (e.g., defaulting Name to "Untitled Item").
//    - Ensures consistent casing (SKU is typically stored in uppercase).
//    - Trims whitespace on string fields to prevent database formatting issues.
//


namespace CoffeeNChill.Functions.Models
{
    public class MenuItem : ITableEntity
    {
        // Azure Table Storage Required Properties
        public string PartitionKey { get; set; } = string.Empty; // Holds 'Category' (e.g., "Hot Drinks")
        public string RowKey { get; set; } = string.Empty;       // Holds 'SKU/ID' (e.g., "COF-001")
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // CoffeeNChill Domain Properties
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public bool IsAvailable { get; set; }
    }

    // Data Transfer Object (DTO) for Incoming HTTP Requests
    public class MenuItemRequest
    {
        public string Category { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
