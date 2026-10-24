using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopSphere.Services.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialIdentityMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "identity");

        migrationBuilder.CreateTable(
            name: "InboxState",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                Received = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReceiveCount = table.Column<int>(type: "int", nullable: false),
                ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                Consumed = table.Column<DateTime>(type: "datetime2", nullable: true),
                Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InboxState", x => x.Id);
                table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OutboxState",
            schema: "identity",
            columns: table => new
            {
                OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
            });

        migrationBuilder.CreateTable(
            name: "Permissions",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RefreshTokens",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TokenHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshTokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessage",
            schema: "identity",
            columns: table => new
            {
                SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EnqueueTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                InboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                InboxConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SourceAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                DestinationAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ResponseAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                FaultAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                table.ForeignKey(
                    name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                    columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                    principalSchema: "identity",
                    principalTable: "InboxState",
                    principalColumns: new[] { "MessageId", "ConsumerId" });
                table.ForeignKey(
                    name: "FK_OutboxMessage_OutboxState_OutboxId",
                    column: x => x.OutboxId,
                    principalSchema: "identity",
                    principalTable: "OutboxState",
                    principalColumn: "OutboxId");
            });

        migrationBuilder.CreateTable(
            name: "RolePermissions",
            schema: "identity",
            columns: table => new
            {
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                table.ForeignKey(
                    name: "FK_RolePermissions_Permissions_PermissionId",
                    column: x => x.PermissionId,
                    principalSchema: "identity",
                    principalTable: "Permissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RolePermissions_Roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserRoles",
            schema: "identity",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_UserRoles_Roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserRoles_Users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InboxState_Delivered",
            schema: "identity",
            table: "InboxState",
            column: "Delivered");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_EnqueueTime",
            schema: "identity",
            table: "OutboxMessage",
            column: "EnqueueTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_ExpirationTime",
            schema: "identity",
            table: "OutboxMessage",
            column: "ExpirationTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
            schema: "identity",
            table: "OutboxMessage",
            columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
            unique: true,
            filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_OutboxId_SequenceNumber",
            schema: "identity",
            table: "OutboxMessage",
            columns: new[] { "OutboxId", "SequenceNumber" },
            unique: true,
            filter: "[OutboxId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_ProcessedAtUtc",
            schema: "identity",
            table: "OutboxMessages",
            column: "ProcessedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxState_Created",
            schema: "identity",
            table: "OutboxState",
            column: "Created");

        migrationBuilder.CreateIndex(
            name: "IX_Permissions_Code",
            schema: "identity",
            table: "Permissions",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_TokenHash",
            schema: "identity",
            table: "RefreshTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserId",
            schema: "identity",
            table: "RefreshTokens",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_PermissionId",
            schema: "identity",
            table: "RolePermissions",
            column: "PermissionId");

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Name",
            schema: "identity",
            table: "Roles",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_RoleId",
            schema: "identity",
            table: "UserRoles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            schema: "identity",
            table: "Users",
            column: "Email",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OutboxMessage",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "OutboxMessages",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "RefreshTokens",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "RolePermissions",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "UserRoles",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "InboxState",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "OutboxState",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "Permissions",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "Roles",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "Users",
            schema: "identity");
    }
}
