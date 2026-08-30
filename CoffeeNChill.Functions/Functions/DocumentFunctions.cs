using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using CoffeeNChill.Functions.Services;
using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Functions;

// HTTP-triggered functions for staff document management (staff-docs File Share).
// Covers upload, list, and download endpoints as required by Part 1 of the POE.
public class DocumentFunctions
{
    private readonly ILogger _logger;
    private readonly DocumentStorageService _storageService;

    public DocumentFunctions(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<DocumentFunctions>();

        // AzureWebJobsStorage is the connection string Azure Functions uses for storage.
        // Locally this comes from local.settings.json.
        string connectionString = Environment.GetEnvironmentVariable("FileShareStorage")
                                  ?? throw new InvalidOperationException("FileShareStorage connection string is not configured.");

        _storageService = new DocumentStorageService(connectionString);
    }

    // POST /api/documents/upload
    // Accepts a file via multipart/form-data (not JSON) and streams it into the
    // staff-docs File Share. 
    [Function("UploadStaffDocument")]
    public async Task<HttpResponseData> UploadStaffDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequestData req)
    {
        // Multipart requests declare a "boundary" string in the Content-Type header,
        // which marks where each part (field/file) of the request body starts and ends.
        var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();

        if (contentType == null || !contentType.Contains("multipart/form-data"))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Request must be multipart/form-data.");
            return badRequest;
        }

        var boundary = MultipartBoundary(contentType);

        // MultipartReader splits the raw request body into individual "sections"
        // using that boundary, one section per uploaded field/file.
        var reader = new MultipartReader(boundary, req.Body);

        MultipartSection? section;
        string? uploadedFileName = null;

        // Loop through every section in the request.
        while ((section = await reader.ReadNextSectionAsync()) != null)
        {
            var contentDisposition = section.Headers?["Content-Disposition"].ToString();

            // Only sections representing an actual uploaded file include a "filename="
            // in their Content-Disposition header.
            if (contentDisposition != null && contentDisposition.Contains("filename="))
            {
                uploadedFileName = ExtractFileName(contentDisposition);

                if (string.IsNullOrWhiteSpace(uploadedFileName))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Uploaded file must have a valid file name.");
                    return badRequest;
                }

                // Buffer the file into memory, then hand the stream off to the storage
                // service which does the actual streaming write into the File Share.
                using var memoryStream = new MemoryStream();
                await section.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0; // reset stream position before it's read again

                await _storageService.UploadFileAsync(uploadedFileName, memoryStream);
            }
        }

        // If looped through every section and never found a file part, the client
        // sent a malformed or empty request.
        if (uploadedFileName == null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("No file was found in the request.");
            return badRequest;
        }

        _logger.LogInformation($"Uploaded staff document: {uploadedFileName}");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync($"File '{uploadedFileName}' uploaded successfully.");
        return response;
    }
    
    // GET /api/documents
    // Lists every file currently stored in the staff-docs File Share, returning
    // each file's name, size, and last modified date as JSON.
    [Function("ListStaffDocuments")]
    public async Task<HttpResponseData> ListStaffDocuments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequestData req)
    {
        // Delegates the actual Azure File Share query to the storage service,
        // keeping this function focused only on the HTTP request/response handling.
        List<StaffDocumentInfo> documents = await _storageService.ListFilesAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);

        // WriteAsJsonAsync automatically serializes the list to JSON and sets
        // the Content-Type header to application/json for us.
        await response.WriteAsJsonAsync(documents);

        return response;
    }
    
    // GET /api/documents/download/{fileName}
    // Streams the requested file back to the client from the staff-docs File Share.
    // {fileName} is taken directly from the URL path (e.g. /api/documents/download/recipe.pdf).
    [Function("DownloadStaffDocument")]
    public async Task<HttpResponseData> DownloadStaffDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/download/{fileName}")] HttpRequestData req,
        string fileName)
    {
        // Ask the storage service for the file's content stream. It returns null
        // if the file doesn't exist, so we can respond with 404 instead of crashing.
        Stream? fileStream = await _storageService.DownloadFileAsync(fileName);

        if (fileStream == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"File '{fileName}' was not found.");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);

        // Setting Content-Disposition tells the client (browser/Postman) this is a
        // downloadable file attachment, and suggests the original file name to save it as.
        response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        response.Headers.Add("Content-Type", "application/octet-stream");

        // Copy the file's bytes directly into the HTTP response body stream.
        await fileStream.CopyToAsync(response.Body);

        return response;
    }

    // Extracts the boundary value from the Content-Type header
    private static string MultipartBoundary(string contentType)
    {
        var elements = contentType.Split(' ');
        var boundaryElement = elements.First(e => e.StartsWith("boundary="));
        return boundaryElement.Substring("boundary=".Length).Trim('"');
    }

    // Pulls the file name out of a Content-Disposition header like:
    // form-data; name="file"; filename="recipe.pdf"
    private static string? ExtractFileName(string contentDisposition)
    {
        var parts = contentDisposition.Split(';');
        var fileNamePart = parts.FirstOrDefault(p => p.Trim().StartsWith("filename="));
        return fileNamePart?.Split('=')[1].Trim('"');
    }
}