using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FitTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBodyPartMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BodyPartMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MeasuredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    BodyPart = table.Column<int>(type: "integer", nullable: false),
                    ValueCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyPartMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BodyPartMeasurements_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BodyPartMeasurements_MeasuredOn",
                table: "BodyPartMeasurements",
                column: "MeasuredOn");

            migrationBuilder.CreateIndex(
                name: "IX_BodyPartMeasurements_UserId",
                table: "BodyPartMeasurements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BodyPartMeasurements_UserId_BodyPart_MeasuredOn",
                table: "BodyPartMeasurements",
                columns: new[] { "UserId", "BodyPart", "MeasuredOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BodyPartMeasurements");
        }
    }
}
