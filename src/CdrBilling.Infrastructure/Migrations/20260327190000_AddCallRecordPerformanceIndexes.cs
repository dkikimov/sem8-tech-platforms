using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CdrBilling.Infrastructure.Migrations
{
    public partial class AddCallRecordPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_cr_session_computed_charge",
                table: "call_records",
                columns: new[] { "session_id", "computed_charge" });

            migrationBuilder.CreateIndex(
                name: "ix_cr_session_start_time",
                table: "call_records",
                columns: new[] { "session_id", "start_time" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cr_session_computed_charge",
                table: "call_records");

            migrationBuilder.DropIndex(
                name: "ix_cr_session_start_time",
                table: "call_records");
        }
    }
}
