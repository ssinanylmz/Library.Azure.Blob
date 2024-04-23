using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Library.Blob.Azure.Models;

namespace Library.Blob.Azure
{
    public class BlobService : IBlobService
    {
        #region Dependency Injection / Constructor

        private readonly BlobServiceClient _blobServiceClient;
        private BlobContainerClient _blobContainerClient;

        public BlobService(IConfiguration configuration)
        {
            _blobServiceClient = new BlobServiceClient(configuration["BlobConnectionString"]);
        }

        #endregion

        public async Task<BlobResponseDto> GetAsync(string fileName, string containerName)
        {
            // Get a reference to a container named in appsettings.json
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            try
            {
                // Get a reference to the blob uploaded earlier from the API in the container from configuration settings
                BlobClient file = _blobContainerClient.GetBlobClient(fileName);

                // Check if the file exists in the container
                if (await file.ExistsAsync())
                {
                    string uri = _blobContainerClient.Uri.ToString();
                    var name = file.Name;
                    var fullUri = $"{uri}/{name}";
                    var data = await file.OpenReadAsync();

                    BlobDto blob = new BlobDto
                    {
                        Content = data,
                        Uri = fullUri,
                        Name = name
                    };

                    return new BlobResponseDto { Error = false, Blob = blob };
                }
            }
            catch (RequestFailedException ex)
                when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
            {
                return new BlobResponseDto { Error = true, Status = string.Format("FILE_NOT_FOUND", fileName) };
            }

            return new BlobResponseDto { Error = true, Status = string.Format("FILE_NOT_FOUND", fileName) };
        }

        public async Task<BlobResponseDto> DeleteAsync(string fileName, string containerName)
        {
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            BlobClient file = _blobContainerClient.GetBlobClient(fileName);

            try
            {
                // Delete the file
                await file.DeleteAsync();
            }
            catch (RequestFailedException ex)
                when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
            {
                // File did not exist, log to console and return new response to requesting method
                //_logger.LogError($"File {blobFilename} was not found.");
                return new BlobResponseDto { Error = true, Status = string.Format("FILE_NOT_FOUND", fileName) };
            }

            // Return a new BlobResponseDto to the requesting method
            return new BlobResponseDto { Error = false, Status = string.Format("FILE_SUCCESS_DELETED", fileName) };
        }

        public async Task<BlobDto> DownloadAsync(string fileName, string containerName)
        {
            // Get a reference to a container named in appsettings.json
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            try
            {
                // Get a reference to the blob uploaded earlier from the API in the container from configuration settings
                BlobClient file = _blobContainerClient.GetBlobClient(fileName);

                // Check if the file exists in the container
                if (await file.ExistsAsync())
                {
                    var data = await file.OpenReadAsync();
                    Stream blobContent = data;

                    // Download the file details async
                    var content = await file.DownloadContentAsync();

                    // Add data to variables in order to return a BlobDto
                    string name = fileName;
                    string contentType = content.Value.Details.ContentType;

                    // Create new BlobDto with blob data from variables
                    return new BlobDto { Content = blobContent, Name = name, ContentType = contentType };
                }
            }
            catch (RequestFailedException ex)
                when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
            {
                // Log error to console
                //_logger.LogError($"File {blobFilename} was not found.");
            }

            // File does not exist, return null and handle that in requesting method
            return null;
        }

        public async Task<List<BlobDto>> ListAsync(string containerName)
        {
            // Get a reference to a container named in appsettings.json
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            // Create a new list object for 
            List<BlobDto> files = new List<BlobDto>();

            await foreach (BlobItem file in _blobContainerClient.GetBlobsAsync())
            {
                // Add each file retrieved from the storage container to the files list by creating a BlobDto object
                string uri = _blobContainerClient.Uri.ToString();
                var name = file.Name;
                var fullUri = $"{uri}/{name}";

                files.Add(new BlobDto
                {
                    Uri = fullUri,
                    Name = name,
                    ContentType = file.Properties.ContentType
                });
            }

            // Return all files to the requesting method
            return files;
        }

        public async Task<BlobResponseDto> UploadAsync(IFormFile formFile, string fileName, string containerName, BlobHttpHeaders headers = null)
        {
            // Create new upload response object that we can return to the requesting method
            BlobResponseDto response = new();

            if (!CheckFileSize(formFile.Length))
            {
                response.Status = "FILE_SIZE_OVER";
                response.Error = true;
                return response;
            }

            var magicNumber = GetMagicNumber(formFile);
            if (!CheckFileByMagicNumber(magicNumber) && !fileName.Contains(".zd"))
            {
                response.Status = "FILE_TYPE_ERROR";
                response.Error = true;
                return response;
            }

            // Get a reference to a container named in appsettings.json and then create it
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            _blobContainerClient.SetAccessPolicy(PublicAccessType.BlobContainer);

            //await container.CreateAsync();
            try
            {
                // Get a reference to the blob just uploaded from the API in a container from configuration settings
                BlobClient client = _blobContainerClient.GetBlobClient(fileName);

                if (await client.ExistsAsync())
                {
                    response.Status = string.Format("FILE_ALREADY_EXISTS", fileName);
                    response.Error = true;
                    return response;
                }

                // Open a stream for the file we want to upload
                await using (Stream? data = formFile.OpenReadStream())
                {
                    // Upload the file async
                    if (headers != null)
                        await client.UploadAsync(data, headers);
                    else
                        await client.UploadAsync(data);
                }

                // Everything is OK and file got uploaded
                response.Status = "FILE_SUCCESS_UPLOADED";
                response.Error = false;
                response.Blob.Uri = client.Uri.AbsoluteUri;
                response.Blob.Name = client.Name;
                response.Blob.Content = await client.OpenReadAsync();

            }
            // If the file already exists, we catch the exception and do not upload it
            catch (RequestFailedException ex)
                when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
            {
                //_logger.LogError($"File with name {blob.FileName} already exists in container. Set another name to store the file in the container: '{_storageContainerName}.'");
                response.Status = string.Format("FILE_ALREADY_EXISTS", fileName);
                response.Error = true;
                return response;
            }
            // If we get an unexpected error, we catch it here and return the error message
            catch (RequestFailedException ex)
            {
                // Log error to console and create a new response we can return to the requesting method
                //_logger.LogError($"Unhandled Exception. ID: {ex.StackTrace} - Message: {ex.Message}");
                response.Status = "GENERAL_ERROR";
                response.Error = true;
                return response;
            }

            // Return the BlobUploadResponse object
            return response;
        }

        #region Private Methods

        private static string GetMagicNumber(IFormFile formFile)
        {
            var chkBinary = new BinaryReader(formFile.OpenReadStream());
            var chkBytes = chkBinary.ReadBytes(0x10);
            var dataAsHex = BitConverter.ToString(chkBytes);
            var magicCheck = dataAsHex.Substring(0, 11);

            return magicCheck;
        }

        private static bool CheckFileByMagicNumber(string magicNumber)
        {
            var magicNumbers = new Dictionary<string, string>()
            {
                {"xls" ,"D0-CF-11-E0"},
                {"doc" ,"D0-CF-11-E0"},
                {"jpeg","FF-D8-FF-E0"},
                {"pdf" ,"FF-D8-FF-E0"},
                {"pdf1" ,"25-50-44-46"},
                {"png" ,"89-50-4E-47"},
                {"xlsx","50-4B-03-04"},
                {"docx","50-4B-03-04"},
                {"txt" ,"61-64-73-61" },
                {"zd" ,"30-82-08-3B" },
                {"zd1" ,"30-82-08-3A" }
            };

            return magicNumbers.Any(x => x.Value == magicNumber);
        }

        private static bool CheckFileSize(long fileLength)
        {
            return fileLength < 5242880;
        }

        #endregion 

    }
}