using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopSphere.Services.Payment.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialPaymentMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "payment");

        migrationBuilder.CreateTable(
            name: "InboxState",
            schema: "payment",
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
            schema: "payment",
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
            schema: "payment",
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
            name: "PaymentAttempts",
            schema: "payment",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ProviderTransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WebhookLogs",
            schema: "payment",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebhookLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessage",
            schema: "payment",
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
                    principalSchema: "payment",
                    principalTable: "InboxState",
                    principalColumns: new[] { "MessageId", "ConsumerId" });
                table.ForeignKey(
                    name: "FK_OutboxMessage_OutboxState_OutboxId",
                    column: x => x.OutboxId,
                    principalSchema: "payment",
                    principalTable: "OutboxState",
                    principalColumn: "OutboxId");
            });

        migrationBuilder.CreateIndex(
            name: "IX_InboxState_Delivered",
            schema: "payment",
            table: "InboxState",
            column: "Delivered");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_EnqueueTime",
            schema: "payment",
            table: "OutboxMessage",
            column: "EnqueueTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_ExpirationTime",
            schema: "payment",
            table: "OutboxMessage",
            column: "ExpirationTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
            schema: "payment",
            table: "OutboxMessage",
            columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
            unique: true,
            filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_OutboxId_SequenceNumber",
            schema: "payment",
            table: "OutboxMessage",
            columns: new[] { "OutboxId", "SequenceNumber" },
            unique: true,
            filter: "[OutboxId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_ProcessedAtUtc",
            schema: "payment",
            table: "OutboxMessages",
            column: "ProcessedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxState_Created",
            schema: "payment",
            table: "OutboxState",
            column: "Created");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentAttempts_OrderId",
            schema: "payment",
            table: "PaymentAttempts",
            column: "OrderId");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentAttempts_ProviderTransactionId",
            schema: "payment",
            table: "PaymentAttempts",
            column: "ProviderTransactionId");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentAttempts_Status",
            schema: "payment",
            table: "PaymentAttempts",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_WebhookLogs_EventId",
            schema: "payment",
            table: "WebhookLogs",
            column: "EventId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OutboxMessage",
            schema: "payment");

        migrationBuilder.DropTable(
            name: "OutboxMessages",
            schema: "payment");

        migrationBuilder.DropTable(
            name: "PaymentAttempts",
            schema: "payment");

        migrationBuilder.DropTable(
            name: "WebhookLogs",
            schema: "payment");

        migrationBuilder.DropTable(
            name: "InboxState",
            schema: "payment");

        migrationBuilder.DropTable(
            name: "OutboxState",
            schema: "payment");
    }
}
