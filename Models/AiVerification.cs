using System;
using System.Collections.Generic;

namespace Intranet.Models;

public partial class AiVerification
{
    public int Id { get; set; }

    public int RequestId { get; set; }

    public string? InvoiceNumber { get; set; }

    public bool IsLegit { get; set; }

    public string MatchStatus { get; set; } = null!;

    public string? DiscrepancyDetails { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public virtual Request Request { get; set; } = null!;
}
