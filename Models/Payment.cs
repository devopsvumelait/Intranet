using System;
using System.Collections.Generic;

namespace Intranet.Models;

public partial class Payment
{
    public int Id { get; set; }

    public int RequestId { get; set; }

    public Guid PaidById { get; set; }

    public DateTime PaymentDate { get; set; }

    public decimal AmountPaid { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? ReferenceNumber { get; set; }

    public string PopBlobUrl { get; set; } = null!;

    public string Status { get; set; } = null!;

    public virtual User PaidBy { get; set; } = null!;

    public virtual Request Request { get; set; } = null!;
}
