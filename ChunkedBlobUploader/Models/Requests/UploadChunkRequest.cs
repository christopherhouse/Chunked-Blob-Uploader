using System;

namespace ChunkedBlobUploader.Models.Requests
{
    public class UploadChunkRequest
    {
        public string Base64ByteArray { get; set; }

        public string FileName { get; set; }

        public bool IsFinalChunk { get; set; }

        public byte[] ContentAsByteArray() => Convert.FromBase64String(Base64ByteArray);
    }
}
