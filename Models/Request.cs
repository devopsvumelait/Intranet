using System;
using System.Collections.Generic;

namespace Intranet.Models;

public partial class Request
{
    public int Id { get; set; }

    public Guid RequesterId { get; set; }

    public string Description { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public string CostType { get; set; } = null!;

    public string PaymentTiming { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? RejectionReason { get; set; }

    public bool IsPoRequired { get; set; } = false;
    public DateTime? FutureDate { get; set; }
    public bool IsOverdue { get; set; } = false;

    public string RequestType { get; set; } = "Normal";
    public string QuoteType { get; set; } = "None";
    public string DepartmentType { get; set; } = "None";

    public string CustomerName { get; set; } = "None";


    public virtual ICollection<AiVerification> AiVerifications { get; set; } = new List<AiVerification>();

    public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Quote> Quotes { get; set; } = new List<Quote>();

    public virtual User Requester { get; set; } = null!;

    
}
