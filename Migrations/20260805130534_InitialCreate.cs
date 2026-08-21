using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intranet.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Proc");

            migrationBuilder.EnsureSchema(
                name: "Core");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AuditLog__3214EC07B30AE5F2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Departme__3214EC0771E95E4A", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Roles__3214EC070DBD3F54", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newsequentialid())"),
                    Email = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Surname = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    PasswordChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__3214EC078173C20E", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Users__Departmen__60A75C0F",
                        column: x => x.DepartmentId,
                        principalSchema: "Core",
                        principalTable: "Departments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                schema: "Proc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "Draft"),
                    CostType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    PaymentTiming = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Present"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPoRequired = table.Column<bool>(type: "bit", nullable: false),
                    FutureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsOverdue = table.Column<bool>(type: "bit", nullable: false),
                    RequestType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuoteType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartmentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Requests__3214EC0716E20DA5", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Requests__Reques__6C190EBB",
                        column: x => x.RequesterId,
                        principalSchema: "Core",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "Core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__UserRole__AF2760AD2302413A", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK__UserRoles__RoleI__693CA210",
                        column: x => x.RoleId,
                        principalSchema: "Core",
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__UserRoles__UserI__68487DD7",
                        column: x => x.UserId,
                        principalSchema: "Core",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AiVerifications",
                schema: "Proc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsLegit = table.Column<bool>(type: "bit", nullable: false),
                    MatchStatus = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    DiscrepancyDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AiVerifi__3214EC072767F918", x => x.Id);
                    table.ForeignKey(
                        name: "FK__AiVerific__Reque__160F4887",
                        column: x => x.RequestId,
                        principalSchema: "Proc",
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                schema: "Proc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    DecisionDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Approval__3214EC0786C3BDE8", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Approvals__Appro__778AC167",
                        column: x => x.ApproverId,
                        principalSchema: "Core",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Approvals__Reque__76969D2E",
                        column: x => x.RequestId,
                        principalSchema: "Proc",
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "Proc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BlobUrl = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: false),
                    DocType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Document__3214EC07D3B174C0", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Documents__Reque__7B5B524B",
                        column: x => x.RequestId,
                        principalSchema: "Proc",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK__Documents__Uploa__7C4F7684",
                        column: x => x.UploadedById,
                        principalSchema: "Core",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    Type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Notifica__3214EC07463E2436", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Notificat__Reque__03F0984C",
                        column: x => x.RequestId,
                        principalSchema: "Proc",
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Notificat__UserI__02FC7413",
                        column: x => x.UserId,
                        principalSchema: "Core",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "Proc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    PaidById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PopBlobUrl = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Completed")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Payments__3214EC072EE57FA6", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Payments__PaidBy__2A164134",
                        column: x => x.PaidById,
                        principalSchema: "Core",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Payments__Reques__29221CFB",
                        column: x => x.RequestId,
                        principalSchema: "Proc",
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "Proc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsSelected = table.Column<bool>(type: "bit", nullable: false),
                    BlobUrl = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: false),
                    DocType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    AiExtractedVat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AiConfidenceScore = table.Column<double>(type: "float", nullable: true),
                    AiAnalysisNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileHash = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Quotes__3214EC075406B476", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Quotes__RequestI__72C60C4A",
                        column: x => x.RequestId,
                        principalSchema: "Proc",
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiVerifications_RequestId",
                schema: "Proc",
                table: "AiVerifications",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_ApproverId",
                schema: "Proc",
                table: "Approvals",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_RequestId",
                schema: "Proc",
                table: "Approvals",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RequestId",
                schema: "Proc",
                table: "Documents",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedById",
                schema: "Proc",
                table: "Documents",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RequestId",
                schema: "Core",
                table: "Notifications",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                schema: "Core",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaidById",
                schema: "Proc",
                table: "Payments",
                column: "PaidById");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RequestId",
                schema: "Proc",
                table: "Payments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_RequestId",
                schema: "Proc",
                table: "Quotes",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RequesterId",
                schema: "Proc",
                table: "Requests",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "UQ__Roles__8A2B6160E74F2CAD",
                schema: "Core",
                table: "Roles",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "Core",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                schema: "Core",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "UQ__Users__A9D10534E2A9FE87",
                schema: "Core",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiVerifications",
                schema: "Proc");

            migrationBuilder.DropTable(
                name: "Approvals",
                schema: "Proc");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "Proc");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "Proc");

            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "Proc");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "Requests",
                schema: "Proc");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "Core");
        }
    }
}
