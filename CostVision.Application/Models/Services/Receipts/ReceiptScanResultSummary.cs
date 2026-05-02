﻿﻿namespace CostVision.Application.Models.Services.Receipts
{
    public class ReceiptScanResultSummary
    {
        public int ScannedCount { get; set; }

        public int AddedToDbCount { get; set; }

        public int ErrorCount { get; set; }

        public List<QrScanResult> Results { get; set; } = new();
    }
}
