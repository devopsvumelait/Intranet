using System;
using System.Collections.Generic;

namespace Intranet.Models;

public partial class AuditLog
{
    public long Id { get; set; }

    public string TableName { get; set; } = null!;

    public string RecordId { get; set; } = null!;

    public Guid ActionBy { get; set; }

    public string ActionType { get; set; } = null!;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public DateTime? Timestamp { get; set; }
}
