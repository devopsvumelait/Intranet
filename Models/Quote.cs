using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Intranet.Models;

public partial class Quote
{
    public int Id { get; set; }

    public int RequestId { get; set; }

    public string SupplierName { get; set; } = null!;

    public decimal Price { get; set; }

    public bool IsSelected { get; set; }

    public string BlobUrl { get; set; } = null!;

    public string DocType { get; set; } = null!;

    public string? AiExtractedVat { get; set; }

    public double? AiConfidenceScore { get; set; }

    public string? AiAnalysisNotes { get; set; }

    [JsonIgnore]
    public virtual Request Request { get; set; } = null!;

    public string? FileHash { get; set; } = "";
}
