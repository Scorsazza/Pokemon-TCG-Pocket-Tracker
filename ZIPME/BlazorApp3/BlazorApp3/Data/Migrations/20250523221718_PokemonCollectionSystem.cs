using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorApp3.Migrations
{
    /// <inheritdoc />
    public partial class PokemonCollectionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "PokemonCards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Pack = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Rarity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CardNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokemonCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserStats",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TotalCards = table.Column<int>(type: "int", nullable: false),
                    UniqueCards = table.Column<int>(type: "int", nullable: false),
                    CompletedSets = table.Column<int>(type: "int", nullable: false),
                    BountiesCompleted = table.Column<int>(type: "int", nullable: false),
                    BountiesPosted = table.Column<int>(type: "int", nullable: false),
                    TradesCompleted = table.Column<int>(type: "int", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStats", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserStats_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BountyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CardId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OfferedAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    OfferedItem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BountyRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BountyRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BountyRequests_PokemonCards_CardId",
                        column: x => x.CardId,
                        principalTable: "PokemonCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CardId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsForTrade = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCards_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCards_PokemonCards_CardId",
                        column: x => x.CardId,
                        principalTable: "PokemonCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BountyOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BountyRequestId = table.Column<int>(type: "int", nullable: false),
                    OffererId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CardId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BountyOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BountyOffers_AspNetUsers_OffererId",
                        column: x => x.OffererId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BountyOffers_BountyRequests_BountyRequestId",
                        column: x => x.BountyRequestId,
                        principalTable: "BountyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BountyOffers_PokemonCards_CardId",
                        column: x => x.CardId,
                        principalTable: "PokemonCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BountyOffers_BountyRequestId",
                table: "BountyOffers",
                column: "BountyRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BountyOffers_CardId",
                table: "BountyOffers",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_BountyOffers_OffererId",
                table: "BountyOffers",
                column: "OffererId");

            migrationBuilder.CreateIndex(
                name: "IX_BountyOffers_Status",
                table: "BountyOffers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BountyRequests_CardId",
                table: "BountyRequests",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_BountyRequests_CreatedAt",
                table: "BountyRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BountyRequests_Status",
                table: "BountyRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BountyRequests_UserId",
                table: "BountyRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PokemonCards_Name",
                table: "PokemonCards",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PokemonCards_Pack",
                table: "PokemonCards",
                column: "Pack");

            migrationBuilder.CreateIndex(
                name: "IX_PokemonCards_Pack_CardNumber",
                table: "PokemonCards",
                columns: new[] { "Pack", "CardNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PokemonCards_Rarity",
                table: "PokemonCards",
                column: "Rarity");

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_CardId",
                table: "UserCards",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_IsForTrade",
                table: "UserCards",
                column: "IsForTrade");

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_UserId",
                table: "UserCards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_UserId_CardId",
                table: "UserCards",
                columns: new[] { "UserId", "CardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserStats_BountiesCompleted",
                table: "UserStats",
                column: "BountiesCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_UserStats_CompletedSets",
                table: "UserStats",
                column: "CompletedSets");

            migrationBuilder.CreateIndex(
                name: "IX_UserStats_UniqueCards",
                table: "UserStats",
                column: "UniqueCards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BountyOffers");

            migrationBuilder.DropTable(
                name: "UserCards");

            migrationBuilder.DropTable(
                name: "UserStats");

            migrationBuilder.DropTable(
                name: "BountyRequests");

            migrationBuilder.DropTable(
                name: "PokemonCards");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "AspNetUsers");
        }
    }
}
