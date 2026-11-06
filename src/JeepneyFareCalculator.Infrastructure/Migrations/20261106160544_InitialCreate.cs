using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JeepneyFareCalculator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FareMatrixVersion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FareMatrixVersion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FareMatrixRow",
                columns: table => new
                {
                    FareMatrixVersionId = table.Column<int>(type: "int", nullable: false),
                    DistanceKm = table.Column<int>(type: "int", nullable: false),
                    RegularFare = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    DiscountedFare = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FareMatrixRow", x => new { x.FareMatrixVersionId, x.DistanceKm });
                    table.ForeignKey(
                        name: "FK_FareMatrixRow_FareMatrixVersion_FareMatrixVersionId",
                        column: x => x.FareMatrixVersionId,
                        principalTable: "FareMatrixVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FareMatrixVersion_VehicleType",
                table: "FareMatrixVersion",
                column: "VehicleType",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FareMatrixRow");

            migrationBuilder.DropTable(
                name: "FareMatrixVersion");
        }
    }
}
