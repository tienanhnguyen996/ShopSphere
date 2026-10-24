using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopSphere.Services.Shipping.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialShippingMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "shipping");

        migrationBuilder.CreateTable(
            name: "InboxState",
            schema: "shipping",
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
            schema: "shipping",
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
            schema: "shipping",
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
            name: "Shipments",
            schema: "shipping",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Carrier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ExternalShipmentRequestId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecipientStreet = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                RecipientCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RecipientState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RecipientPostalCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                RecipientCountry = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                RecipientPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                HandedOverAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Shipments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessage",
            schema: "shipping",
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
                    principalSchema: "shipping",
                    principalTable: "InboxState",
                    principalColumns: new[] { "MessageId", "ConsumerId" });
                table.ForeignKey(
                    name: "FK_OutboxMessage_OutboxState_OutboxId",
                    column: x => x.OutboxId,
                    principalSchema: "shipping",
                    principalTable: "OutboxState",
                    principalColumn: "OutboxId");
            });

        migrationBuilder.CreateTable(
            name: "ShipmentItems",
            schema: "shipping",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ProductName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ShipmentItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_ShipmentItems_Shipments_ShipmentId",
                    column: x => x.ShipmentId,
                    principalSchema: "shipping",
                    principalTable: "Shipments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TrackingMilestones",
            schema: "shipping",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackingMilestones", x => x.Id);
                table.ForeignKey(
                    name: "FK_TrackingMilestones_Shipments_ShipmentId",
                    column: x => x.ShipmentId,
                    principalSchema: "shipping",
                    principalTable: "Shipments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InboxState_Delivered",
            schema: "shipping",
            table: "InboxState",
            column: "Delivered");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_EnqueueTime",
            schema: "shipping",
            table: "OutboxMessage",
            column: "EnqueueTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_ExpirationTime",
            schema: "shipping",
            table: "OutboxMessage",
            column: "ExpirationTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
            schema: "shipping",
            table: "OutboxMessage",
            columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
            unique: true,
            filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_OutboxId_SequenceNumber",
            schema: "shipping",
            table: "OutboxMessage",
            columns: new[] { "OutboxId", "SequenceNumber" },
            unique: true,
            filter: "[OutboxId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_ProcessedAtUtc",
            schema: "shipping",
            table: "OutboxMessages",
            column: "ProcessedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxState_Created",
            schema: "shipping",
            table: "OutboxState",
            column: "Created");

        migrationBuilder.CreateIndex(
            name: "IX_ShipmentItems_ShipmentId",
            schema: "shipping",
            table: "ShipmentItems",
            column: "ShipmentId");

        migrationBuilder.CreateIndex(
            name: "IX_Shipments_OrderId",
            schema: "shipping",
            table: "Shipments",
            column: "OrderId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Shipments_TrackingNumber",
            schema: "shipping",
            table: "Shipments",
            column: "TrackingNumber");

        migrationBuilder.CreateIndex(
            name: "IX_TrackingMilestones_ShipmentId",
            schema: "shipping",
            table: "TrackingMilestones",
            column: "ShipmentId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OutboxMessage",
            schema: "shipping");

        migrationBuilder.DropTable(
            name: "OutboxMessages",
            schema: "shipping");

        migrationBuilder.DropTable(
            name: "ShipmentItems",
            schema: "shipping");

        migrationBuilder.DropTable(
            name: "TrackingMilestones",
            schema: "shipping");

        migrationBuilder.DropTable(
            name: "InboxState",
            schema: "shipping");

        migrationBuilder.DropTable(
            name: "OutboxState",
            schema: "shipping");

        migrationBuilder.DropTable(
            name: "Shipments",
            schema: "shipping");
    }
}
