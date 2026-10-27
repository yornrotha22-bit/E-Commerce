using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RothaShop.Data;

#nullable disable

namespace RothaShop.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261024130000_AddProductVariantImageUrl")]
public partial class AddProductVariantImageUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ImageUrl",
            table: "ProductVariants",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ImageUrl",
            table: "ProductVariants");
    }
}
