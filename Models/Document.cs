using System;
using System.Collections.Generic;

namespace Intranet.Models;

public partial class Document
{
    public int Id { get; set; }

    public int? RequestId { get; set; }

    public string FileName { get; set; } = null!;

    public string BlobUrl { get; set; } = null!;

    public string DocType { get; set; } = null!;

    public Guid UploadedById { get; set; }

    public DateTime? UploadedAt { get; set; }

    public virtual Request Request { get; set; } = null!;

    public virtual User UploadedBy { get; set; } = null!;
}
