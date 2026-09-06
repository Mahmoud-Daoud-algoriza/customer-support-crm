using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasksAndInternalNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketInternalNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketInternalNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketInternalNotes_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketInternalNotes_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDone = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTasks_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketTasks_Users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketTasks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_InternalNoteId",
                table: "TicketActivities",
                column: "InternalNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketInternalNotes_AuthorUserId",
                table: "TicketInternalNotes",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketInternalNotes_TicketId_CreatedAt",
                table: "TicketInternalNotes",
                columns: new[] { "TicketId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketTasks_AssignedUserId_IsDone_DueAt",
                table: "TicketTasks",
                columns: new[] { "AssignedUserId", "IsDone", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketTasks_CreatedByUserId",
                table: "TicketTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTasks_TicketId",
                table: "TicketTasks",
                column: "TicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketActivities_TicketInternalNotes_InternalNoteId",
                table: "TicketActivities",
                column: "InternalNoteId",
                principalTable: "TicketInternalNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketActivities_TicketInternalNotes_InternalNoteId",
                table: "TicketActivities");

            migrationBuilder.DropTable(
                name: "TicketInternalNotes");

            migrationBuilder.DropTable(
                name: "TicketTasks");

            migrationBuilder.DropIndex(
                name: "IX_TicketActivities_InternalNoteId",
                table: "TicketActivities");
        }
    }
}
