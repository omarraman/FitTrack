using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FitTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPelvicFloorTraining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PelvicFloorPrograms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PelvicFloorPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PelvicFloorPrograms_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PelvicFloorDailyCheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PelvicFloorProgramId = table.Column<int>(type: "integer", nullable: false),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PelvicTensionRating = table.Column<int>(type: "integer", nullable: true),
                    HasPainOrUrinarySymptoms = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PelvicFloorDailyCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PelvicFloorDailyCheckIns_PelvicFloorPrograms_PelvicFloorPro~",
                        column: x => x.PelvicFloorProgramId,
                        principalTable: "PelvicFloorPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PelvicFloorSessionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PelvicFloorProgramId = table.Column<int>(type: "integer", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    CompletedSustainedSets = table.Column<int>(type: "integer", nullable: false),
                    CompletedSustainedRepsPerSet = table.Column<int>(type: "integer", nullable: false),
                    CompletedHoldSeconds = table.Column<int>(type: "integer", nullable: false),
                    CompletedRestSeconds = table.Column<int>(type: "integer", nullable: false),
                    CompletedQuickReps = table.Column<int>(type: "integer", nullable: false),
                    CompletedBreathingFinish = table.Column<bool>(type: "boolean", nullable: false),
                    EffortRating = table.Column<int>(type: "integer", nullable: true),
                    RelaxationRating = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PelvicFloorSessionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PelvicFloorSessionLogs_PelvicFloorPrograms_PelvicFloorProgr~",
                        column: x => x.PelvicFloorProgramId,
                        principalTable: "PelvicFloorPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PelvicFloorDailyCheckIns_CheckInDate",
                table: "PelvicFloorDailyCheckIns",
                column: "CheckInDate");

            migrationBuilder.CreateIndex(
                name: "IX_PelvicFloorDailyCheckIns_PelvicFloorProgramId_CheckInDate",
                table: "PelvicFloorDailyCheckIns",
                columns: new[] { "PelvicFloorProgramId", "CheckInDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PelvicFloorPrograms_StartDate",
                table: "PelvicFloorPrograms",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_PelvicFloorPrograms_UserId",
                table: "PelvicFloorPrograms",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PelvicFloorPrograms_UserId_Status",
                table: "PelvicFloorPrograms",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PelvicFloorSessionLogs_PelvicFloorProgramId_SessionDate",
                table: "PelvicFloorSessionLogs",
                columns: new[] { "PelvicFloorProgramId", "SessionDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PelvicFloorSessionLogs_SessionDate",
                table: "PelvicFloorSessionLogs",
                column: "SessionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PelvicFloorDailyCheckIns");

            migrationBuilder.DropTable(
                name: "PelvicFloorSessionLogs");

            migrationBuilder.DropTable(
                name: "PelvicFloorPrograms");
        }
    }
}
