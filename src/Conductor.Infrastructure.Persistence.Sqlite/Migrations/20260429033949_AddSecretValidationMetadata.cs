using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conductor.Infrastructure.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSecretValidationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidatedAtUtc",
                table: "SecretDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationMessage",
                table: "SecretDescriptors",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationMetadataJson",
                table: "SecretDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationStatus",
                table: "SecretDescriptors",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotValidated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidatedAtUtc",
                table: "SecretDescriptors");

            migrationBuilder.DropColumn(
                name: "ValidationMessage",
                table: "SecretDescriptors");

            migrationBuilder.DropColumn(
                name: "ValidationMetadataJson",
                table: "SecretDescriptors");

            migrationBuilder.DropColumn(
                name: "ValidationStatus",
                table: "SecretDescriptors");
        }
    }
}
