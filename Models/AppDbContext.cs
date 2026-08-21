using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Intranet.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AiVerification> AiVerifications { get; set; }

    public virtual DbSet<Approval> Approvals { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Quote> Quotes { get; set; }

    public virtual DbSet<Request> Requests { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserRole> UserRoles { get; set; }

   protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiVerification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AiVerifi__3214EC072767F918");

            entity.ToTable("AiVerifications", "Proc");

            entity.Property(e => e.InvoiceNumber).HasMaxLength(100);
            entity.Property(e => e.MatchStatus)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.VerifiedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Request).WithMany(p => p.AiVerifications)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AiVerific__Reque__160F4887");
        });

        modelBuilder.Entity<Approval>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Approval__3214EC0786C3BDE8");

            entity.ToTable("Approvals", "Proc");

            entity.Property(e => e.DecisionDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Stage)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Approver).WithMany(p => p.Approvals)
                .HasForeignKey(d => d.ApproverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Approvals__Appro__778AC167");

            entity.HasOne(d => d.Request).WithMany(p => p.Approvals)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Approvals__Reque__76969D2E");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AuditLog__3214EC07B30AE5F2");

            entity.ToTable("AuditLogs", "Core");

            entity.Property(e => e.ActionType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RecordId).HasMaxLength(100);
            entity.Property(e => e.TableName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Departme__3214EC0771E95E4A");

            entity.ToTable("Departments", "Core");

            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Document__3214EC07D3B174C0");

            entity.ToTable("Documents", "Proc");

            entity.Property(e => e.BlobUrl)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.DocType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("(getdate())");

           entity.HasOne(d => d.Request).WithMany(p => p.Documents)
                .HasForeignKey(d => d.RequestId)
                .IsRequired(false) 
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__Documents__Reque__7B5B524B");

            entity.HasOne(d => d.UploadedBy).WithMany(p => p.Documents)
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Documents__Uploa__7C4F7684");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC07463E2436");

            entity.ToTable("Notifications", "Core");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(100);
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Request).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RequestId)
                .HasConstraintName("FK__Notificat__Reque__03F0984C");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__UserI__02FC7413");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Payments__3214EC072EE57FA6");

            entity.ToTable("Payments", "Proc");

            entity.Property(e => e.AmountPaid).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PopBlobUrl)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Completed");

            entity.HasOne(d => d.PaidBy).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PaidById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__PaidBy__2A164134");

            entity.HasOne(d => d.Request).WithMany(p => p.Payments)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__Reques__29221CFB");
        });

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Quotes__3214EC075406B476");

            entity.ToTable("Quotes", "Proc");

            entity.Property(e => e.AiExtractedVat).HasMaxLength(50).IsUnicode(true);
            entity.Property(e => e.BlobUrl)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.DocType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SupplierName).HasMaxLength(200);

            entity.HasOne(d => d.Request).WithMany(p => p.Quotes)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Quotes__RequestI__72C60C4A");
        });

        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Requests__3214EC0716E20DA5");

            entity.ToTable("Requests", "Proc");

            entity.Property(e => e.CostType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.PaymentTiming)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Present");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Draft");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Requester).WithMany(p => p.Requests)
                .HasForeignKey(d => d.RequesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Requests__Reques__6C190EBB");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Roles__3214EC070DBD3F54");

            entity.ToTable("Roles", "Core");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B6160E74F2CAD").IsUnique();

            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC078173C20E");

            entity.ToTable("Users", "Core");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534E2A9FE87").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.FirstName).HasMaxLength(20);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Surname).HasMaxLength(20);

            entity.HasOne(d => d.Department).WithMany(p => p.Users)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__Departmen__60A75C0F");

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
            .UsingEntity<UserRole>(
            r => r.HasOne<Role>(e => e.Role).WithMany(e => e.UserRoles)
            .HasForeignKey(d => d.RoleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__UserRoles__RoleI__693CA210"),
            l => l.HasOne<User>(e => e.User).WithMany(e => e.UserRoles)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__UserRoles__UserI__68487DD7"),
                j =>
                {
                    j.HasKey(ur => new { ur.UserId, ur.RoleId }).HasName("PK__UserRole__AF2760AD2302413A");
                    j.ToTable("UserRoles", "Core");
                });

                });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
