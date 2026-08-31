using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TicketMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketMessages_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketMessages_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_MessageId",
                table: "TicketActivities",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_AuthorUserId",
                table: "TicketMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_TicketId_PostedAt",
                table: "TicketMessages",
                columns: new[] { "TicketId", "PostedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_TicketActivities_TicketMessages_MessageId",
                table: "TicketActivities",
                column: "MessageId",
                principalTable: "TicketMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketActivities_TicketMessages_MessageId",
                table: "TicketActivities");

            migrationBuilder.DropTable(
                name: "TicketMessages");

            migrationBuilder.DropIndex(
                name: "IX_TicketActivities_MessageId",
                table: "TicketActivities");
        }
    }
}
