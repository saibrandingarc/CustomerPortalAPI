using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace zohodeliverablesAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StaffType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                });
            migrationBuilder.CreateTable(
            name: "Settings",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"), // Auto-incrementing primary key
                ZohoToken = table.Column<string>(nullable: true),
                RefreshToken = table.Column<string>(nullable: true),
                Scope = table.Column<string>(nullable: true),
                DateAdded = table.Column<DateTime>(nullable: false), // This field cannot be null
                ExpiresIn = table.Column<int>(nullable: false) // This field cannot be null
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Settings", x => x.Id); // Primary key constraint
            });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Staff");
        }
    }
}
