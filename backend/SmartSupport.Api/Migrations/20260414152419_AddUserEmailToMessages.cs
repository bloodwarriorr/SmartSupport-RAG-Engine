using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSupport.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEmailToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserEmail",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserEmail",
                table: "ChatMessages");
        }
    }
}
