﻿using CostVision.Application.Models.Services.Receipts;

namespace CostVision.Application.Models.Requests.Receipts
{
    public class QrScanRequest
    {
        public Guid AccountId { get; set; }
        public List<QrScanResult> Results { get; set; } = new();
    }
}
