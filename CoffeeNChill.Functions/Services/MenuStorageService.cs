using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Logging;


// MENU STORAGE SERVICE - Data Access Layer
//This code was completed by Neha ST10478910
//Double Checked by Douglass St10473980

//
// This file handles all direct interactions with the Azure Table Storage database.
// It abstracts the low-level storage operations away from the API controllers.
//
// Key functionalities include:
//
// 1. Database Operations (Azure Table Storage):
//    - Create:   Adds a new MenuItem entity to the table.
//    - Read All: Retrieves all entities from the table (potentially with filtering).
//    - Read by Category: Queries the table for all items matching a specific PartitionKey (Category).
//    - Read Single: Retrieves a single entity by its exact PartitionKey (Category) and RowKey (SKU).
//    - Update:   Replaces an existing entity in the table.
//    - Delete:   Removes an entity from the table based on its composite key.
//
// 2. Data Mapping:
//    - Handles converting HTTP request models into Azure Table Storage entities (and vice versa).
//    - Ensures keys are formatted correctly (e.g., trimming whitespace, converting SKU to uppercase).
//
// 3. Error Handling & Logging:
//    - Uses ILogger to log successful operations, warnings, and critical database failures.
//    - Throws specific exceptions (like KeyNotFoundException or InvalidOperationException)
//      back up to the MenuFunctions layer so the API can return appropriate HTTP status codes.
//
// ======================================================================================

namespace CoffeeNChill.Functions.Services
{
    public class MenuStorageService
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<MenuStorageService>? _logger;

        public MenuStorageService(string connectionString, ILogger<MenuStorageService>? logger = null)
        {
            _logger = logger;

            try
            {
                // Initializes TableServiceClient and creates the "MenuItems" table if it doesn't exist yet
                var serviceClient = new TableServiceClient(connectionString);
                _tableClient = serviceClient.GetTableClient("MenuItems");
                _tableClient.CreateIfNotExists();
                _logger?.LogInformation("MenuItems table initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize MenuItems table.");
                throw;
            }
        }

        // 1. Create a new menu item
        public async Task<MenuItem> CreateMenuItemAsync(MenuItem item)
        {
            try
            {
                await _tableClient.AddEntityAsync(item);
                _logger?.LogInformation($"Created menu item: {item.RowKey} in category {item.PartitionKey}");
                return item;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                _logger?.LogWarning($"Menu item {item.RowKey} already exists.");
                throw new InvalidOperationException($"Menu item with SKU '{item.RowKey}' already exists.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to create menu item: {item.RowKey}");
                throw;
            }
        }

        // 2. Get all menu items
        public async Task<List<MenuItem>> GetAllMenuItemsAsync()
        {
            var items = new List<MenuItem>();
            try
            {
                AsyncPageable<MenuItem> queryResults = _tableClient.QueryAsync<MenuItem>(_ => true);
                await foreach (var item in queryResults)
                {
                    items.Add(item);
                }
                _logger?.LogInformation($"Retrieved {items.Count} menu items.");
            }
            catch (RequestFailedException ex)
            {
                _logger?.LogError(ex, "Failed to retrieve menu items.");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error retrieving menu items.");
                throw;
            }
            return items;
        }

        // 3. Get menu items by Category (PartitionKey)
        public async Task<List<MenuItem>> GetMenuItemsByCategoryAsync(string category)
        {
            var items = new List<MenuItem>();
            try
            {
                AsyncPageable<MenuItem> queryResults = _tableClient.QueryAsync<MenuItem>(
                    item => item.PartitionKey == category);
                await foreach (var item in queryResults)
                {
                    items.Add(item);
                }
                _logger?.LogInformation($"Retrieved {items.Count} items for category '{category}'.");
            }
            catch (RequestFailedException ex)
            {
                _logger?.LogError(ex, $"Failed to retrieve items for category '{category}'.");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Unexpected error retrieving items for category '{category}'.");
                throw;
            }
            return items;
        }

        // 4. Get a single menu item by Category & SKU
        public async Task<MenuItem?> GetMenuItemAsync(string category, string sku)
        {
            try
            {
                Response<MenuItem> response = await _tableClient.GetEntityAsync<MenuItem>(category, sku);
                _logger?.LogInformation($"Retrieved menu item: {sku} in category {category}");
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger?.LogInformation($"Menu item {sku} in category {category} not found.");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error retrieving menu item: {sku} in category {category}");
                throw;
            }
        }

        // 5. Update an existing menu item
        public async Task UpdateMenuItemAsync(MenuItem item)
        {
            try
            {
                await _tableClient.UpdateEntityAsync(item, ETag.All, TableUpdateMode.Replace);
                _logger?.LogInformation($"Updated menu item: {item.RowKey} in category {item.PartitionKey}");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger?.LogWarning($"Cannot update - menu item {item.RowKey} not found.");
                throw new KeyNotFoundException($"Menu item '{item.RowKey}' not found.");
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                _logger?.LogWarning($"Update failed - concurrency conflict for menu item {item.RowKey}.");
                throw new InvalidOperationException($"Menu item '{item.RowKey}' was modified by another process. Please refresh and try again.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error updating menu item: {item.RowKey}");
                throw;
            }
        }

        // 6. Delete a menu item
        public async Task DeleteMenuItemAsync(string category, string sku)
        {
            try
            {
                await _tableClient.DeleteEntityAsync(category, sku);
                _logger?.LogInformation($"Deleted menu item: {sku} in category {category}");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger?.LogWarning($"Cannot delete - menu item {sku} not found.");
                throw new KeyNotFoundException($"Menu item '{sku}' not found.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error deleting menu item: {sku} in category {category}");
                throw;
            }
        }
    }
}
