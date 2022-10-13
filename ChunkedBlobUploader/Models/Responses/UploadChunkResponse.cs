using System.Collections.Generic;

namespace ChunkedBlobUploader.Models.Responses
{
    public class UploadChunkResponse
    {
        public UploadChunkResponse()
        {
            UploadedBlockIds = new List<string>();
        }

        public List<string> UploadedBlockIds { get; set; }
    }
}
