using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conductor.Infrastructure.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class PersistNormalizedInstanceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationName",
                table: "InstanceSnapshots",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationVersion",
                table: "InstanceSnapshots",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "InstanceSnapshots",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HttpStatusCode",
                table: "InstanceSnapshots",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LatencyMilliseconds",
                table: "InstanceSnapshots",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersistenceProvider",
                table: "InstanceSnapshots",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeDefaultsJson",
                table: "InstanceSnapshots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeInstanceId",
                table: "InstanceSnapshots",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowOwner",
                table: "InstanceSnapshots",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowRepository",
                table: "InstanceSnapshots",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowSourcePath",
                table: "InstanceSnapshots",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationName",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "ApplicationVersion",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "HttpStatusCode",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "LatencyMilliseconds",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "PersistenceProvider",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "RuntimeDefaultsJson",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "RuntimeInstanceId",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "WorkflowOwner",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "WorkflowRepository",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "WorkflowSourcePath",
                table: "InstanceSnapshots");
        }
    }
}
