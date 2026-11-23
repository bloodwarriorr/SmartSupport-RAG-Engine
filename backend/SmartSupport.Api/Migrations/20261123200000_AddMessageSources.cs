using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSupport.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sources",
                table: "ChatMessages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sources",
                table: "ChatMessages");
        }
    }
}
