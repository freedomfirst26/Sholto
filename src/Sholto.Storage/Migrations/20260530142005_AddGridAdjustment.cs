using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sholto.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddGridAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GridAdjustments",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BpmOverride = table.Column<double>(type: "REAL", nullable: true),
                    OffsetSec = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GridAdjustments", x => x.TrackId);
                    table.ForeignKey(
                        name: "FK_GridAdjustments_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GridAdjustments");
        }
    }
}
