using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CoffeeNChill.Functions.DTO;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

//This code was completed by Neha ST10478910
//Double Checked by Douglass St10473980
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
//             GET  /api/categories/{category} (Retrieves items by category)
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
        /// <summary>
        /// Handles POST requests to create a new menu item in the database.
        /// Validates that the Category and SKU are provided, and that the SKU matches
        /// the required format of XXX-000 (e.g., COF-001). Returns a 201 Created response
        /// with the newly created item, or a 400 Bad Request if validation fails.
        /// </summary>
        /// <param name="req">The incoming HTTP request containing the menu item JSON in the body.</param>
        /// <returns>An HTTP response with the created item or an appropriate error message.</returns>
        [Function("CreateMenuItem")]
        public async Task<HttpResponseData> CreateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")]
            HttpRequestData req)
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

                // - Douglass ST10473980 Code Start 
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return await CreateBadResponse(req, "Name is required.");
                }

                if (request.Price <= 0)
                {
                    return await CreateBadResponse(req, "Price must be greater than zero.");
                }
                // - Douglass ST10473980 Code End 

                //Douglass -ST10473980 Btter validation
                if (!IsValidSku(request.SKU))
                {
                    return await CreateBadResponse(
                        req,
                        "SKU must follow format: XXX-000 (e.g., COF-001)");
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
        /// <summary>
        /// Handles GET requests to retrieve every menu item stored in the database.
        /// Returns a 200 OK response with a JSON array of all items, or a 500 error
        /// if the storage service fails.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <returns>An HTTP response with a JSON array of all menu items.</returns>
        [Function("GetAllMenuItems")]
        public async Task<HttpResponseData> GetAllMenuItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")]
            HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Getting all menu items.");

                var items = await _storageService.GetAllMenuItemsAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
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

        // 3. GET /api/categories/{category} - Get items by category
        // IMPORTANT: The route lives under "categories/" (not "menu/...") to avoid a
        // routing conflict with "menu/{category}/{sku}". If it were under "menu/",
        // the Azure Functions router would interpret "by-category" as the {category}
        // parameter and "Drinks" as the {sku} parameter, sending the request to the
        // wrong function entirely.
        /// <summary>
        /// Handles GET requests to retrieve all menu items that belong to a specific category.
        /// Returns a 200 OK response with a filtered JSON array of items. The route is
        /// intentionally placed under /api/categories/ (not /api/menu/) to avoid a routing
        /// conflict with the GetMenuItem endpoint, which uses the pattern /api/menu/{category}/{sku}.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <param name="category">The category name to filter items by (e.g., "Drinks").</param>
        /// <returns>An HTTP response with a JSON array of items in that category, or 400 if the category is empty.</returns>

        [Function("GetMenuItemsByCategory")]
        public async Task<HttpResponseData> GetMenuItemsByCategory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")]
            HttpRequestData req,
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
        /// <summary>
        /// Handles GET requests to retrieve a single menu item using its composite key
        /// of Category (PartitionKey) and SKU (RowKey). Returns a 200 OK response with
        /// the item's data if found, or a 404 Not Found if no matching item exists.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <param name="category">The category of the item to retrieve.</param>
        /// <param name="sku">The unique SKU of the item to retrieve.</param>
        /// <returns>An HTTP response with the requested item, or 404 if it doesn't exist.</returns>
        [Function("GetMenuItem")]
        public async Task<HttpResponseData> GetMenuItem(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "get",
                Route = "menu/item/{category}/{sku}"
            )]
            HttpRequestData req,
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
        /// <summary>
        /// Handles PUT requests to update an existing menu item. Supports partial updates,
        /// meaning only the fields provided in the request body (Name, Description, Price,
        /// IsAvailable) will be changed. Returns a 200 OK with the updated item if successful,
        /// 404 Not Found if the item doesn't exist, or 400 Bad Request if validation fails.
        /// </summary>
        /// <param name="req">The incoming HTTP request containing the update data in the body.</param>
        /// <param name="category">The category of the item to update.</param>
        /// <param name="sku">The SKU of the item to update.</param>
        /// <returns>An HTTP response with the updated item or an appropriate error message.</returns>
        [Function("UpdateMenuItem")]
        public async Task<HttpResponseData> UpdateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{sku}")]
            HttpRequestData req,
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

                //Changed DTO to UpdateMenuItem
                var request = JsonSerializer.Deserialize<UpdateMenuItemRequest>(requestBody, _jsonOptions);

                if (request == null)
                {
                    return await CreateBadResponse(req, "Invalid request body.");
                }
                // Douglass - St10473980 Fixed start
                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    existingItem.Name = request.Name.Trim();
                }

                if (request.Description != null)
                {
                    existingItem.Description = request.Description.Trim();
                }

                if (request.Price.HasValue)
                {
                    if (request.Price.Value <= 0)
                    {
                        return await CreateBadResponse(
                            req,
                            "Price must be greater than zero."
                        );
                    }

                    existingItem.Price = request.Price.Value;
                }

                if (request.IsAvailable.HasValue)
                {
                    existingItem.IsAvailable = request.IsAvailable.Value;
                }
                // Douglass - St10473980 Fixed End
                await _storageService.UpdateMenuItemAsync(existingItem);

                var response = req.CreateResponse(HttpStatusCode.OK);
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
        /// <summary>
        /// Handles DELETE requests to remove a specific menu item from the database using
        /// its Category (PartitionKey) and SKU (RowKey). Returns 204 No Content on success,
        /// or 404 Not Found if the item does not exist.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <param name="category">The category of the item to delete.</param>
        /// <param name="sku">The SKU of the item to delete.</param>
        /// <returns>An HTTP response indicating success (204 No Content) or 404 if not found.</returns>
        [Function("DeleteMenuItem")]
        public async Task<HttpResponseData> DeleteMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{sku}")]
            HttpRequestData req,
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
        /// <summary>
        /// Handles GET requests to the health check endpoint. Returns a 200 OK with a
        /// JSON payload containing the API status, current UTC timestamp, service name,
        /// and version. Used for uptime monitoring, load balancer checks, and quick
        /// smoke tests to verify the API is running.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <returns>An HTTP response with the current health status of the API as JSON.</returns>
        [Function("HealthCheck")]
        public async Task<HttpResponseData> HealthCheck(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
            HttpRequestData req)
        {
            _logger.LogInformation("Health check requested.");
            var response = req.CreateResponse(HttpStatusCode.OK);

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
            string json = JsonSerializer.Serialize(new { error = "An unexpected error occurred. Please try again." },
                _jsonOptions);
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
