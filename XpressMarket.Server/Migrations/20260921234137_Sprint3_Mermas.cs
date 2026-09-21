using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XpressMarket.Server.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_Mermas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsMerma",
                table: "Lotes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaMerma",
                table: "Lotes",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsMerma",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "FechaMerma",
                table: "Lotes");
        }
    }
}
