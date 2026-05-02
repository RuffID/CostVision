﻿using CostVision.Application.Models.Services.Receipts;

namespace CostVision.Application.Models.Requests.Receipts
{
    public class ReceiptManualCreateRequest
    {
        public ManualReceiptInput Receipt { get; set; } = new();
        public Guid AccountId { get; set; }
    }        
}