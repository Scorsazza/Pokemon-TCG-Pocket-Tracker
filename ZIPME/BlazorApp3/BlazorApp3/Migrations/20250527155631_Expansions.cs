using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp3.Migrations
{
    /// <inheritdoc />
    public partial class Expansions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Expansion",
                table: "PokemonCards",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Expansion",
                table: "PokemonCards");
        }
    }
}
