using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;


// MENU FUNCTIONS - API Controller (used chat to help me with the explaination) 

//
// This file handles all HTTP endpoints related to the CoffeeNChill menu. 
// It acts as the entry point for client requests.
//
// Key functionalities include:
//
// 1. CRUD Operations:
//    - Create: POST /api/menu (Adds a new item with validation)
//    - Read:   GET  /api/menu (Retrieves all items)
//             GET  /api/menu/category/{category} (Retrieves items by category)
//             GET  /api/menu/{category}/{sku} (Retrieves a single specific item)
//    - Update: PUT  /api/menu/{category}/{sku} (Modifies an existing item, supports partial updates)
//    - Delete: DELETE /api/menu/{category}/{sku} (Removes an item)
//
// 2. Additional Endpoints:
//    - Health Check: GET /api/health (Used for monitoring API uptime)
//
// 3. Error Handling:
//    - Contains standardized helper methods to return consistent JSON error responses
//      for 400 (Bad Request), 404 (Not Found), and 500 (Internal Server Error).
//
// 4. Data Validation:
//    - Ensures required fields like Category and SKU are provided.
//    - Uses a regex helper to enforce SKU format: XXX-000 (e.g., COF-001).
//    - Applies case-insensitive and camelCase JSON serialization options.
//
// ======================================================================================

namespace CoffeeNChill.Functions.Functions
{
    public class MenuFunctions
    {
        private readonly ILogger<MenuFunctions> _logger;
        private readonly MenuStorageService _storageService;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Dependency Injection - The framework provides both logger and storage service
        public MenuFunctions(
            ILogger<MenuFunctions> logger,
            MenuStorageService storageService)
        {
            _logger = logger;
            _storageService = storageService;
        }

        // 1. POST /api/menu - Create a new menu item
        [Function("CreateMenuItem")]
        public async Task<HttpResponseData> CreateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Creating a new menu item.");

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<MenuItemRequest>(requestBody, _jsonOptions);

                if (request == null)
                {
                    return await CreateBadResponse(req, "Invalid request body.");
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.Category))
                {
                    return await CreateBadResponse(req, "Category is required.");
                }

                if (string.IsNullOrWhiteSpace(request.SKU))
                {
                    return await CreateBadResponse(req, "SKU is required.");
                }

                // Validate SKU format (optional but good practice)
                if (!IsValidSku(request.SKU))
                {
                    return await CreateBadResponse(req, "SKU must follow format: XXX-000 (e.g., COF-001)");
                }

                var menuItem = new MenuItem
                {
                    PartitionKey = request.Category.Trim(),
                    RowKey = request.SKU.Trim().ToUpper(),
                    Name = request.Name?.Trim() ?? "Untitled Item",
                    Description = request.Description?.Trim() ?? string.Empty,
                    Price = request.Price > 0 ? request.Price : 0,
                    IsAvailable = request.IsAvailable
                };

                var created = await _storageService.CreateMenuItemAsync(menuItem);

                var response = req.CreateResponse(HttpStatusCode.Created);
                // FIX: Manually serialize to JSON and write to the body
                string json = JsonSerializer.Serialize(created, _jsonOptions);
                await response.WriteStringAsync(json, Encoding.UTF8);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                return response;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex.Message);
                return await CreateBadResponse(req, ex.Message);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON received.");
                return await CreateBadResponse(req, "Invalid JSON format. Please check your request body.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating menu item.");
                return await CreateErrorResponse(req);
            }
        }

        // 2. GET /api/menu - Get all menu items
        [Function("GetAllMenuItems")]
        public async Task<HttpResponseData> GetAllMenuItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Getting all menu items.");

                var items = await _storageService.GetAllMenuItemsAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                // FIX: Manually serialize to JSON and write to the body
                string json = JsonSerializer.Serialize(items, _jsonOptions);
                await response.WriteStringAsync(json, Encoding.UTF8);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving menu items.");
                return await CreateErrorResponse(req);
            }
        }

        // 3. GET /api/menu/category/{category} - Get items by category
        [Function("GetMenuItemsByCategory")]
        public async Task<HttpResponseData> GetMenuItemsByCategory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")] HttpRequestData req,
            string category)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(category))
                {
                    return await CreateBadResponse(req, "Category cannot be empty.");
                }

                _logger.LogInformation($"Getting menu items for category: {category}");

                var items = await _storageService.GetMenuItemsByCategoryAsync(category.Trim());

                var response = req.CreateResponse(HttpStatusCode.OK);
                // FIX: Manually serialize to JSON and write to the body
                string json = JsonSerializer.Serialize(items, _jsonOptions);
                await response.WriteStringAsync(json, Encoding.UTF8);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving items for category: {category}");
                return await CreateErrorResponse(req);
            }
        }

        // 4. GET /api/menu/{category}/{sku} - Get single menu item
        [Function("GetMenuItem")]
        public async Task<HttpResponseData> GetMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/{category}/{sku}")] HttpRequestData req,
            string category,
            string sku)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(sku))
                {
                    return await CreateBadResponse(req, "Category and SKU are required.");
                }

                _logger.LogInformation($"Getting item with Category: {category}, SKU: {sku}");

                var item = await _storageService.GetMenuItemAsync(category.Trim(), sku.Trim().ToUpper());

                if (item == null)
                {
                    return await CreateNotFoundResponse(req, $"Menu item '{sku}' in category '{category}' not found.");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                // FIX: Manually serialize to JSON and write to the body
                string json = JsonSerializer.Serialize(item, _jsonOptions);
                await response.WriteStringAsync(json, Encoding.UTF8);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving item: {category}/{sku}");
                return await CreateErrorResponse(req);
            }
        }

        // 5. PUT /api/menu/{category}/{sku} - Update menu item
        [Function("UpdateMenuItem")]
        public async Task<HttpResponseData> UpdateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{sku}")] HttpRequestData req,
            string category,
            string sku)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(sku))
                {
                    return await CreateBadResponse(req, "Category and SKU are required.");
                }

                _logger.LogInformation($"Updating menu item Category: {category}, SKU: {sku}");

                var existingItem = await _storageService.GetMenuItemAsync(category.Trim(), sku.Trim().ToUpper());
                if (existingItem == null)
                {
                    return await CreateNotFoundResponse(req, $"Menu item '{sku}' in category '{category}' not found.");
                }

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<MenuItemRequest>(requestBody, _jsonOptions);

                if (request == null)
                {
                    return await CreateBadResponse(req, "Invalid request body.");
                }

                // Only update fields that are provided (partial update support)
                if (!string.IsNullOrWhiteSpace(request.Name))
                    existingItem.Name = request.Name.Trim();

                if (!string.IsNullOrWhiteSpace(request.Description))
                    existingItem.Description = request.Description.Trim();

                if (request.Price > 0)
                    existingItem.Price = request.Price;

                // Explicitly set availability
                existingItem.IsAvailable = request.IsAvailable;

                await _storageService.UpdateMenuItemAsync(existingItem);

                var response = req.CreateResponse(HttpStatusCode.OK);
                // FIX: Manually serialize to JSON and write to the body
                string json = JsonSerializer.Serialize(existingItem, _jsonOptions);
                await response.WriteStringAsync(json, Encoding.UTF8);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                return response;
            }
            catch (KeyNotFoundException ex)
            {
                return await CreateNotFoundResponse(req, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex.Message);
                return await CreateBadResponse(req, ex.Message);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON received.");
                return await CreateBadResponse(req, "Invalid JSON format. Please check your request body.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating item: {category}/{sku}");
                return await CreateErrorResponse(req);
            }
        }

        // 6. DELETE /api/menu/{category}/{sku} - Delete menu item
        [Function("DeleteMenuItem")]
        public async Task<HttpResponseData> DeleteMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{sku}")] HttpRequestData req,
            string category,
            string sku)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(sku))
                {
                    return await CreateBadResponse(req, "Category and SKU are required.");
                }

                _logger.LogInformation($"Deleting menu item Category: {category}, SKU: {sku}");

                var existingItem = await _storageService.GetMenuItemAsync(category.Trim(), sku.Trim().ToUpper());
                if (existingItem == null)
                {
                    return await CreateNotFoundResponse(req, $"Menu item '{sku}' in category '{category}' not found.");
                }

                await _storageService.DeleteMenuItemAsync(category.Trim(), sku.Trim().ToUpper());

                var response = req.CreateResponse(HttpStatusCode.NoContent);
                return response;
            }
            catch (KeyNotFoundException ex)
            {
                return await CreateNotFoundResponse(req, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting item: {category}/{sku}");
                return await CreateErrorResponse(req);
            }
        }

        // 7. GET /api/health - Health check endpoint (bonus)
        [Function("HealthCheck")]
        public async Task<HttpResponseData> HealthCheck(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
        {
            _logger.LogInformation("Health check requested.");
            var response = req.CreateResponse(HttpStatusCode.OK);

            // FIX: Manually serialize to JSON and write to the body
            var healthData = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Service = "CoffeeNChill Menu API",
                Version = "v1.0"
            };
            string json = JsonSerializer.Serialize(healthData, _jsonOptions);
            await response.WriteStringAsync(json, Encoding.UTF8);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            return response;
        }


        // Private Helper Methods


        private async Task<HttpResponseData> CreateBadResponse(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            string json = JsonSerializer.Serialize(new { error = message }, _jsonOptions);
            await response.WriteStringAsync(json, Encoding.UTF8);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            return response;
        }

        private async Task<HttpResponseData> CreateNotFoundResponse(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            string json = JsonSerializer.Serialize(new { error = message }, _jsonOptions);
            await response.WriteStringAsync(json, Encoding.UTF8);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            return response;
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            string json = JsonSerializer.Serialize(new { error = "An unexpected error occurred. Please try again." }, _jsonOptions);
            await response.WriteStringAsync(json, Encoding.UTF8);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            return response;
        }

        private static bool IsValidSku(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return false;

            // Format: XXX-000 (3 letters, dash, 3 digits)
            return System.Text.RegularExpressions.Regex.IsMatch(sku.Trim(), @"^[A-Z]{3}-\d{3}$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
