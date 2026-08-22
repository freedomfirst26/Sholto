using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sholto.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddCratesAndMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Crates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Markers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrackId = table.Column<string>(type: "TEXT", nullable: false),
                    PositionSecs = table.Column<double>(type: "REAL", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Markers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Markers_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrateTracks",
                columns: table => new
                {
                    CrateId = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrateTracks", x => new { x.CrateId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_CrateTracks_Crates_CrateId",
                        column: x => x.CrateId,
                        principalTable: "Crates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrateTracks_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarkerLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromMarkerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToMarkerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Transition = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkerLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarkerLinks_Markers_FromMarkerId",
                        column: x => x.FromMarkerId,
                        principalTable: "Markers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarkerLinks_Markers_ToMarkerId",
                        column: x => x.ToMarkerId,
                        principalTable: "Markers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Crates_Name",
                table: "Crates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrateTracks_TrackId",
                table: "CrateTracks",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkerLinks_FromMarkerId",
                table: "MarkerLinks",
                column: "FromMarkerId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkerLinks_ToMarkerId",
                table: "MarkerLinks",
                column: "ToMarkerId");

            migrationBuilder.CreateIndex(
                name: "IX_Markers_TrackId",
                table: "Markers",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrateTracks");

            migrationBuilder.DropTable(
                name: "MarkerLinks");

            migrationBuilder.DropTable(
                name: "Crates");

            migrationBuilder.DropTable(
                name: "Markers");
        }
    }
}
