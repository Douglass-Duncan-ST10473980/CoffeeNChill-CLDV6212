using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services;

public class DocumentStorageService
{
    private const string ShareName = "staff-docs";
    private readonly ShareClient _shareClient;

    public DocumentStorageService(string connectionString)
    {
        _shareClient = new ShareClient(connectionString, ShareName);
        _shareClient.CreateIfNotExists();
    }

    // Uploads a file stream into the staff-docs file share
    public async Task UploadFileAsync(string fileName, Stream fileStream)
    {
        ShareDirectoryClient rootDir = _shareClient.GetRootDirectoryClient();
        ShareFileClient fileClient = rootDir.GetFileClient(fileName);

        await fileClient.CreateAsync(fileStream.Length);
        await fileClient.UploadRangeAsync(
            new Azure.HttpRange(0, fileStream.Length),
            fileStream);
    }

    // Lists all files currently in the staff-docs file share
    public async Task<List<StaffDocumentInfo>> ListFilesAsync()
    {
        var results = new List<StaffDocumentInfo>();
        ShareDirectoryClient rootDir = _shareClient.GetRootDirectoryClient();

        await foreach (ShareFileItem item in rootDir.GetFilesAndDirectoriesAsync())
        {
            if (!item.IsDirectory)
            {
                ShareFileClient fileClient = rootDir.GetFileClient(item.Name);
                ShareFileProperties props = await fileClient.GetPropertiesAsync();

                results.Add(new StaffDocumentInfo
                {
                    FileName = item.Name,
                    SizeInBytes = props.ContentLength,
                    LastModified = props.LastModified
                });
            }
        }

        return results;
    }

    // Downloads a file's content stream from the staff-docs file share
    public async Task<Stream?> DownloadFileAsync(string fileName)
    {
        ShareDirectoryClient rootDir = _shareClient.GetRootDirectoryClient();
        ShareFileClient fileClient = rootDir.GetFileClient(fileName);

        if (!await fileClient.ExistsAsync())
        {
            return null;
        }

        ShareFileDownloadInfo download = await fileClient.DownloadAsync();
        return download.Content;
    }
}