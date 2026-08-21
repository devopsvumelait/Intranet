using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Sas;

namespace Intranet.Services
{
    public class AzureBlobService : IAzureBlobService
    {
       // private readonly string _connectionString;
        private readonly BlobServiceClient _blobServiceClient;

        public AzureBlobService(IConfiguration configuration)
        {
            var connectionString = configuration["BlobConnectionString"]
                ?? configuration.GetConnectionString("BlobConnectionString");

            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        public AzureBlobService(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
        }

        public async Task<string> UploadFileAsync(string containerName, string fileName, Stream fileStream, string contentType)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLower());
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(fileName);

            fileStream.Position = 0;
            await blobClient.UploadAsync(fileStream, true);

            

            return blobClient.Uri.ToString();
        }

        public async Task<Stream> DownloadFileAsync(string containerName, string fileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLower());
            var blobClient = containerClient.GetBlobClient(fileName);

            if (await blobClient.ExistsAsync())
            {
                var response = await blobClient.DownloadAsync();
                return response.Value.Content;
            }

            return null;
        }

        public async Task<bool> DeleteFileAsync(string containerName, string fileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLower());
            var blobClient = containerClient.GetBlobClient(fileName);

            return await blobClient.DeleteIfExistsAsync();
        }

        public string GetReadSasUrl(string containerName, string blobName, int expiryMinutes = 30)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLower());
            var blobClient = containerClient.GetBlobClient(blobName);

            if (blobClient.CanGenerateSasUri)
            {
                var sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = containerName.ToLower(),
                    BlobName = blobName,
                    Resource = "b",
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                    ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

              
                sasBuilder.ContentDisposition = $"inline; filename=\"{blobName}\"";


                string ext = Path.GetExtension(blobName).ToLowerInvariant();
                sasBuilder.ContentType = ext switch
                {
                    ".pdf" => "application/pdf",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".txt" => "text/plain",
                    _ => "application/octet-stream"
                };

                var sasUri = blobClient.GenerateSasUri(sasBuilder);
                return sasUri.ToString();
            }

            return blobClient.Uri.ToString();
        }
    }
}