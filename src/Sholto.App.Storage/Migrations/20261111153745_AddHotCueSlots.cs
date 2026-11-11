using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sholto.App.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddHotCueSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Colour",
                table: "Markers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Slot",
                table: "Markers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Markers_TrackId_Slot",
                table: "Markers",
                columns: new[] { "TrackId", "Slot" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Markers_TrackId_Slot",
                table: "Markers");

            migrationBuilder.DropColumn(
                name: "Colour",
                table: "Markers");

            migrationBuilder.DropColumn(
                name: "Slot",
                table: "Markers");
        }
    }
}
