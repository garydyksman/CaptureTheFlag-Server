using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaptureTheFlag.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DevicePlayerAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameDevices_GameId",
                table: "GameDevices");

            migrationBuilder.AddColumn<byte>(
                name: "AssignedDeviceId",
                table: "GameDevices",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "CombatScore",
                table: "GameDevices",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "EnemyFlagId",
                table: "GameDevices",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Team",
                table: "GameDevices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameDevices_GameId_PlayerName",
                table: "GameDevices",
                columns: new[] { "GameId", "PlayerName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameDevices_GameId_PlayerName",
                table: "GameDevices");

            migrationBuilder.DropColumn(
                name: "AssignedDeviceId",
                table: "GameDevices");

            migrationBuilder.DropColumn(
                name: "CombatScore",
                table: "GameDevices");

            migrationBuilder.DropColumn(
                name: "EnemyFlagId",
                table: "GameDevices");

            migrationBuilder.DropColumn(
                name: "Team",
                table: "GameDevices");

            migrationBuilder.CreateIndex(
                name: "IX_GameDevices_GameId",
                table: "GameDevices",
                column: "GameId");
        }
    }
}
