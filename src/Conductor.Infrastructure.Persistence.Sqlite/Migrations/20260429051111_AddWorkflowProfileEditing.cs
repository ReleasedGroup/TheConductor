using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conductor.Infrastructure.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowProfileEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "WorkflowProfiles",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "WorkflowProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "WorkflowProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "WorkflowProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql(
                """
                UPDATE WorkflowProfiles
                SET Revision = 1,
                    UpdatedAtUtc = CreatedAtUtc;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowProfileId",
                table: "SymphonyInstances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowProfiles_IsDefault",
                table: "WorkflowProfiles",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_WorkflowProfileId",
                table: "SymphonyInstances",
                column: "WorkflowProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_SymphonyInstances_WorkflowProfiles_WorkflowProfileId",
                table: "SymphonyInstances",
                column: "WorkflowProfileId",
                principalTable: "WorkflowProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SymphonyInstances_WorkflowProfiles_WorkflowProfileId",
                table: "SymphonyInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowProfiles_IsDefault",
                table: "WorkflowProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SymphonyInstances_WorkflowProfileId",
                table: "SymphonyInstances");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "WorkflowProfiles");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "WorkflowProfiles");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "WorkflowProfiles");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "WorkflowProfiles");

            migrationBuilder.DropColumn(
                name: "WorkflowProfileId",
                table: "SymphonyInstances");
        }
    }
}
