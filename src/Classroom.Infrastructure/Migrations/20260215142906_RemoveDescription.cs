using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Classroom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "ResourceFiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ResourceFiles",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
