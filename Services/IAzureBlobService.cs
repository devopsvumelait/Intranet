using System.IO;
using System.Threading.Tasks;

namespace Intranet.Services
{
    public interface IAzureBlobService
    {
        Task<string> UploadFileAsync(string containerName, string fileName, Stream fileStream, string contentType);
        Task<Stream> DownloadFileAsync(string containerName, string fileName);
        Task<bool> DeleteFileAsync(string containerName, string fileName);

        string GetReadSasUrl(string containerName, string fileName, int expiryMinutes = 30);
    }
}