using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace zohodeliverablesAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddMainMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.CreateTable(
            //    name: "MainMenu",
            //    columns: table => new
            //    {
            //        Id = table.Column<int>(type: "int", nullable: false)
            //            .Annotation("SqlServer:Identity", "1, 1"),
            //        MainContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        Project = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        Credits = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //        PrimaryDistribution = table.Column<string>(type: "nvarchar(max)", nullable: true),
            //        OtherDistribution = table.Column<string>(type: "nvarchar(max)", nullable: true),
            //        TaskType = table.Column<string>(type: "nvarchar(max)", nullable: true)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_MainMenu", x => x.Id);
            //    });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
        }
    }
}
