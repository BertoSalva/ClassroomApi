using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Classroom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ResourceFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ResourceFiles",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "ResourceFiles");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ResourceFiles");
        }
    }
}
