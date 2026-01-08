using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookShoppingCartMvcUI.Migrations
{
    /// <inheritdoc />
    public partial class AddNavigationFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserInteractions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UserInteractions_BookId",
                table: "UserInteractions",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInteractions_UserId",
                table: "UserInteractions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserInteractions_AspNetUsers_UserId",
                table: "UserInteractions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserInteractions_Book_BookId",
                table: "UserInteractions",
                column: "BookId",
                principalTable: "Book",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserInteractions_AspNetUsers_UserId",
                table: "UserInteractions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserInteractions_Book_BookId",
                table: "UserInteractions");

            migrationBuilder.DropIndex(
                name: "IX_UserInteractions_BookId",
                table: "UserInteractions");

            migrationBuilder.DropIndex(
                name: "IX_UserInteractions_UserId",
                table: "UserInteractions");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserInteractions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
