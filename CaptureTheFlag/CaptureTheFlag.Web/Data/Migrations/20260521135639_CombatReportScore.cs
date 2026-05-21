using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaptureTheFlag.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class CombatReportScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CombatReportScore",
                table: "GameDevices",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CombatReportScore",
                table: "GameDevices");
        }
    }
}
