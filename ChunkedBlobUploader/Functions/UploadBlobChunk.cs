using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ChunkedBlobUploader.Models;
using ChunkedBlobUploader.Models.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace ChunkedBlobUploader.Functions
{
    public class UploadBlobChunk
    {
        private readonly ILogger<UploadBlobChunk> _logger;

        public UploadBlobChunk(ILogger<UploadBlobChunk> log)
        {
            _logger = log;
        }

        [FunctionName(nameof(UploadBlobChunk))]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "uploadblobchunk/{fileName}")] HttpRequest req,
            string fileName,
            [Table("%chunktable%", Connection = "blobStorageConnectionString")] TableClient chunks,
            [Table("%chunktable%")] IAsyncCollector<BlobChunkRecord> chunkToAdd)
        {
            UploadChunkRequest uploadRequest = null;
            using (var streamReader = new StreamReader(req.Body))
            {
                var jsonString = await streamReader.ReadToEndAsync();
                uploadRequest = JsonConvert.DeserializeObject<UploadChunkRequest>(jsonString);
            }
            var newBlockId = await UploadChunk(fileName, uploadRequest.ContentAsByteArray(), uploadRequest.IsFinalChunk);
            var uploadedChunks = await GetBlobChunksByPartitionKey(fileName, chunks);

            var blockIdsToReturn = new List<string>();

            if (uploadedChunks.Any())
            {
                blockIdsToReturn.AddRange(blockIdsToReturn);
            }

            blockIdsToReturn.Add(newBlockId);

            if (uploadRequest.IsFinalChunk)
            {
                await CommitBlobChunks(fileName, blockIdsToReturn, "application/zip");
            }

            return new OkObjectResult(blockIdsToReturn);
        }

        private static async Task<IEnumerable<BlobChunkRecord>> GetBlobChunksByPartitionKey(string partitionKey,
            TableClient chunkTable)
        {
            var blocks = new List<BlobChunkRecord>();
            var queryResults = chunkTable.QueryAsync<BlobChunkRecord>($"PartitionKey eq '{partitionKey}'");
            await foreach (var chunk in queryResults)
            {
                blocks.Add(chunk);
            }

            return blocks;
        }

        private static async Task CommitBlobChunks(string fileName, IList<string> blockIds, string mimeType)
        {
            var options = new CommitBlockListOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = mimeType
                }
            };
            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable(""));
            var containerClient = blobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable(""));
            var blockClient = containerClient.GetBlockBlobClient(fileName);
            await blockClient.CommitBlockListAsync(blockIds, options);
        }

        private static async Task<string> UploadChunk(string fileName, byte[] blobChunkContentBytes, bool isFinalChunk)
        {
            var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            var blobServiceClient =
                new BlobServiceClient(Environment.GetEnvironmentVariable("blobStorageConnectionString"));
            var containerName = Environment.GetEnvironmentVariable("blobContainer");
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blockClient = blobContainerClient.GetBlockBlobClient(fileName);

            using var ms = new MemoryStream(blobChunkContentBytes);
            await blockClient.StageBlockAsync(blockId, ms);

            return blockId;
        }
    }
}

