using System;
using System.Collections.Generic;

namespace Intranet.Models;

public partial class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public int DepartmentId { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? PasswordChangedAt { get; set; }
    public bool MustChangePassword { get; set; }

    public string? PasswordHash { get; set; }

    public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();


}
