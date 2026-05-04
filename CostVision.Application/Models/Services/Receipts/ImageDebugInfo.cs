﻿namespace CostVision.Application.Models.Services.Receipts
{
    public class ImageDebugInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }

        public long FileSizeBytes { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
    }
}
