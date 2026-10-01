using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaptureTheFlag.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlagNodesAndWinner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "FlagKey",
                table: "GameDevices",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "WinnerId",
                table: "Games",
                type: "smallint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlagKey",
                table: "GameDevices");

            migrationBuilder.DropColumn(
                name: "WinnerId",
                table: "Games");
        }
    }
}
