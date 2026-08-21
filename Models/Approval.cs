using System;
using System.Collections.Generic;

namespace Intranet.Models;

public partial class Approval
{
    public int Id { get; set; }

    public int RequestId { get; set; }

    public Guid ApproverId { get; set; }

    public string Stage { get; set; } = null!;

    public bool IsApproved { get; set; }

    public DateTime? DecisionDate { get; set; }

    public string? Comments { get; set; }

    public virtual User Approver { get; set; } = null!;

    public virtual Request Request { get; set; } = null!;
}
