using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimsIntake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePolicyNumber1ToClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PolicyNumber1",
                table: "Claims");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PolicyNumber1",
                table: "Claims",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
