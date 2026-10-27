using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RothaShop.Data;

#nullable disable

namespace RothaShop.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261025090000_AddBakongPaymentQrFields")]
public partial class AddBakongPaymentQrFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "QrExpiresAt",
            table: "Payments",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "QrMd5",
            table: "Payments",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "QrPayload",
            table: "Payments",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "QrExpiresAt", table: "Payments");
        migrationBuilder.DropColumn(name: "QrMd5", table: "Payments");
        migrationBuilder.DropColumn(name: "QrPayload", table: "Payments");
    }
}
