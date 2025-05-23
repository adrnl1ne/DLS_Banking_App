﻿using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class TransactionDocument
{
    [JsonPropertyName("TransactionId")]
    public required string TransactionId { get; set; }

    [JsonPropertyName("Amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("TransactionType")]
    public required string TransactionType { get; set; }

    [JsonPropertyName("Timestamp")]
    public required string Timestamp { get; set; }
}