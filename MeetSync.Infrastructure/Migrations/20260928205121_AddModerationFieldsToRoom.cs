using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationFieldsToRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLobbyEnabled",
                table: "Rooms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Rooms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RoomPassword",
                table: "Rooms",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Recordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recordings_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Summaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SummaryText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyDecisionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Summaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Summaries_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transcripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpeakerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SpeakerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transcripts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transcripts_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_RoomId",
                table: "Recordings",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Summaries_RoomId",
                table: "Summaries",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Transcripts_RoomId",
                table: "Transcripts",
                column: "RoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recordings");

            migrationBuilder.DropTable(
                name: "Summaries");

            migrationBuilder.DropTable(
                name: "Transcripts");

            migrationBuilder.DropColumn(
                name: "IsLobbyEnabled",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "RoomPassword",
                table: "Rooms");
        }
    }
}
