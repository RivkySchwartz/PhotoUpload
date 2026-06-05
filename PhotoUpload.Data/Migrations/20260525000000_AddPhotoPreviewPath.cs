using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoUpload.Data.Migrations
{
    [Migration("20260525000000_AddPhotoPreviewPath")]
    public partial class AddPhotoPreviewPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviewPath",
                table: "Photos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewPath",
                table: "Photos");
        }
    }
}
