using System;
using System.Collections.Generic;

namespace Intranet.Models;

public partial class Notification
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public int? RequestId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string Type { get; set; } = null!;

    public virtual Request? Request { get; set; }

    public virtual User User { get; set; } = null!;
}
