using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopSphere.Services.Catalog.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCatalogMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "catalog");

        migrationBuilder.CreateTable(
            name: "InboxState",
            schema: "catalog",
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
            schema: "catalog",
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
            schema: "catalog",
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
            name: "PriceHistory",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                OldPriceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                NewPriceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PriceVersion = table.Column<int>(type: "int", nullable: false),
                ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PriceHistory", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Products",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                PriceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                PriceVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessage",
            schema: "catalog",
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
                    principalSchema: "catalog",
                    principalTable: "InboxState",
                    principalColumns: new[] { "MessageId", "ConsumerId" });
                table.ForeignKey(
                    name: "FK_OutboxMessage_OutboxState_OutboxId",
                    column: x => x.OutboxId,
                    principalSchema: "catalog",
                    principalTable: "OutboxState",
                    principalColumn: "OutboxId");
            });

        migrationBuilder.CreateIndex(
            name: "IX_InboxState_Delivered",
            schema: "catalog",
            table: "InboxState",
            column: "Delivered");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_EnqueueTime",
            schema: "catalog",
            table: "OutboxMessage",
            column: "EnqueueTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_ExpirationTime",
            schema: "catalog",
            table: "OutboxMessage",
            column: "ExpirationTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
            schema: "catalog",
            table: "OutboxMessage",
            columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
            unique: true,
            filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_OutboxId_SequenceNumber",
            schema: "catalog",
            table: "OutboxMessage",
            columns: new[] { "OutboxId", "SequenceNumber" },
            unique: true,
            filter: "[OutboxId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_ProcessedAtUtc",
            schema: "catalog",
            table: "OutboxMessages",
            column: "ProcessedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxState_Created",
            schema: "catalog",
            table: "OutboxState",
            column: "Created");

        migrationBuilder.CreateIndex(
            name: "IX_PriceHistory_ProductId",
            schema: "catalog",
            table: "PriceHistory",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_PriceHistory_Sku",
            schema: "catalog",
            table: "PriceHistory",
            column: "Sku");

        migrationBuilder.CreateIndex(
            name: "IX_Products_Sku",
            schema: "catalog",
            table: "Products",
            column: "Sku",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OutboxMessage",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "OutboxMessages",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "PriceHistory",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "Products",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "InboxState",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "OutboxState",
            schema: "catalog");
    }
}
