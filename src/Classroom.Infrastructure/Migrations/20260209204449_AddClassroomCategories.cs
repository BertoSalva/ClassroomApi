using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Classroom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Categories",
                table: "ClassroomGroups",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb"); // <-- default for existing rows
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categories",
                table: "ClassroomGroups");
        }
    }
}
