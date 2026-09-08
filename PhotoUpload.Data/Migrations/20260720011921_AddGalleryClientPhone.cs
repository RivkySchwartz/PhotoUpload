using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoUpload.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGalleryClientPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientPhone",
                table: "Galleries",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientPhone",
                table: "Galleries");
        }
    }
}
