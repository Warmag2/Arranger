using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Faemiyah.Arranger.Data.Migrations
{
    /// <inheritdoc />
    public partial class Schedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FinalTimeOptionId",
                table: "Arrangements",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPollClosed",
                table: "Arrangements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TimeOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArrangementId = table.Column<long>(type: "bigint", nullable: false),
                    IsWholeDay = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateBegin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeOptions_Arrangements_ArrangementId",
                        column: x => x.ArrangementId,
                        principalTable: "Arrangements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimeOptions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeOptionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Answer = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Votes_TimeOptions_TimeOptionId",
                        column: x => x.TimeOptionId,
                        principalTable: "TimeOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Arrangements_FinalTimeOptionId",
                table: "Arrangements",
                column: "FinalTimeOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOptions_ArrangementId",
                table: "TimeOptions",
                column: "ArrangementId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOptions_CreatedByUserId",
                table: "TimeOptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOptions_DateBegin",
                table: "TimeOptions",
                column: "DateBegin");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOptions_DateEnd",
                table: "TimeOptions",
                column: "DateEnd");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_TimeOptionId_UserId",
                table: "Votes",
                columns: new[] { "TimeOptionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId",
                table: "Votes",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Arrangements_TimeOptions_FinalTimeOptionId",
                table: "Arrangements",
                column: "FinalTimeOptionId",
                principalTable: "TimeOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Arrangements_TimeOptions_FinalTimeOptionId",
                table: "Arrangements");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "TimeOptions");

            migrationBuilder.DropIndex(
                name: "IX_Arrangements_FinalTimeOptionId",
                table: "Arrangements");

            migrationBuilder.DropColumn(
                name: "FinalTimeOptionId",
                table: "Arrangements");

            migrationBuilder.DropColumn(
                name: "IsPollClosed",
                table: "Arrangements");
        }
    }
}
