using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RothaShop.Data;

#nullable disable

namespace RothaShop.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261024120000_AddLastLoginDevice")]
public partial class AddLastLoginDevice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LastLoginDevice",
            table: "AspNetUsers",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastLoginDevice",
            table: "AspNetUsers");
    }
}
