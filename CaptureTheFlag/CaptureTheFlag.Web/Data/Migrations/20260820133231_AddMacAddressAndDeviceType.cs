using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaptureTheFlag.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMacAddressAndDeviceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeviceType",
                table: "GameDevices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MacAddress",
                table: "GameDevices",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "GameDevices");

            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "GameDevices");
        }
    }
}
