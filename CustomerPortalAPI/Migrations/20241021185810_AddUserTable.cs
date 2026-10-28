using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace zohodeliverablesAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.CreateTable(
            //    name: "Settings",
            //    columns: table => new
            //    {
            //        Id = table.Column<int>(type: "int", nullable: false)
            //            .Annotation("SqlServer:Identity", "1, 1"),
            //        ZohoToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
            //        RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
            //        Scope = table.Column<string>(type: "nvarchar(max)", nullable: true),
            //        DateAdded = table.Column<DateTime>(type: "datetime2", nullable: false),
            //        ExpiresIn = table.Column<int>(type: "int", nullable: false)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_Settings", x => x.Id);
            //    });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Otp = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    ZohoEmailStatus = table.Column<bool>(type: "bit", nullable: true),
                    OTPStatus = table.Column<bool>(type: "bit", nullable: true),
                    Auth0Status = table.Column<bool>(type: "bit", nullable: true),
                    Auth0AccountId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CompanyId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropTable(
            //    name: "Settings");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
