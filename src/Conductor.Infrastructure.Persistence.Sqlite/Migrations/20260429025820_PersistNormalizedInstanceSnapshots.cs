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
            migrationBuilder.AddColumn<int>(
                name: "ActiveIssueCount",
                table: "InstanceSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.AddColumn<int>(
                name: "FailedRunCount",
                table: "InstanceSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PersistenceProvider",
                table: "InstanceSnapshots",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryQueueCount",
                table: "InstanceSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RunningSessionCount",
                table: "InstanceSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.AddColumn<long>(
                name: "TokenInputTotal",
                table: "InstanceSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TokenOutputTotal",
                table: "InstanceSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

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
                name: "ActiveIssueCount",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "ApplicationName",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "ApplicationVersion",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "FailedRunCount",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "PersistenceProvider",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "RetryQueueCount",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "RunningSessionCount",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "RuntimeDefaultsJson",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "RuntimeInstanceId",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "TokenInputTotal",
                table: "InstanceSnapshots");

            migrationBuilder.DropColumn(
                name: "TokenOutputTotal",
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
