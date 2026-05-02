﻿namespace CostVision.Application.Models.Services.Receipts
{
    public class QrScanResult
    {
        public string FileName { get; set; } = string.Empty;

        public string? DecodedText { get; set; }

        public string? ErrorMessage { get; set; }

        public QrParsed? Parsed { get; set; }

        public ImageDebugInfo? DebugInfo { get; set; }

        public DateTime? PhotoDateTime { get; set; }
    }
}
