using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conductor.Infrastructure.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OwnerName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecretDescriptors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ScopeType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SecretType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RotatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecretDescriptors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SymphonyReleaseArtifacts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ReleaseTag = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AssetName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TargetRuntime = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Checksum = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    DownloadedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymphonyReleaseArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ExecutionMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    WorkflowSource = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ProjectId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DefaultBranch = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CloneUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    WebUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    OpenIssueCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PullRequestCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositories_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ProjectId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    ReportType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    GeneratedByUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ContentMarkdown = table.Column<string>(type: "TEXT", nullable: true),
                    ContentHtml = table.Column<string>(type: "TEXT", nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reports_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reports_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SymphonyInstances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    WorkflowProfileId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExecutionMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    HealthStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DeliveryStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReleaseSelector = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ResolvedReleaseTag = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    GitHubSecretId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    OpenAiSecretId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastHealthCheckAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymphonyInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SymphonyInstances_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SymphonyInstances_WorkflowProfiles_WorkflowProfileId",
                        column: x => x.WorkflowProfileId,
                        principalTable: "WorkflowProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TrackedIssues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    GitHubIssueNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SymphonyStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsBlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LabelsJson = table.Column<string>(type: "TEXT", nullable: true),
                    AssigneesJson = table.Column<string>(type: "TEXT", nullable: true),
                    PullRequestsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackedIssues_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundOperations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SymphonyInstanceId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    OperationType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackgroundOperations_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BackgroundOperations_SymphonyInstances_SymphonyInstanceId",
                        column: x => x.SymphonyInstanceId,
                        principalTable: "SymphonyInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SymphonyInstanceId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Events_SymphonyInstances_SymphonyInstanceId",
                        column: x => x.SymphonyInstanceId,
                        principalTable: "SymphonyInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InstanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SymphonyInstanceId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    HealthStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    LatencyMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    HealthJson = table.Column<string>(type: "TEXT", nullable: true),
                    RuntimeJson = table.Column<string>(type: "TEXT", nullable: true),
                    StateJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstanceSnapshots_SymphonyInstances_SymphonyInstanceId",
                        column: x => x.SymphonyInstanceId,
                        principalTable: "SymphonyInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SymphonyInstanceId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    TrackedIssueId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_SymphonyInstances_SymphonyInstanceId",
                        column: x => x.SymphonyInstanceId,
                        principalTable: "SymphonyInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_TrackedIssues_TrackedIssueId",
                        column: x => x.TrackedIssueId,
                        principalTable: "TrackedIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SymphonyInstanceId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    TrackedIssueId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    GitHubIssueNumber = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PullRequestUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Runs_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Runs_SymphonyInstances_SymphonyInstanceId",
                        column: x => x.SymphonyInstanceId,
                        principalTable: "SymphonyInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Runs_TrackedIssues_TrackedIssueId",
                        column: x => x.TrackedIssueId,
                        principalTable: "TrackedIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RunAttempts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RunId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LogPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunAttempts_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_RepositoryId",
                table: "Alerts",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Status_Severity_CreatedAtUtc",
                table: "Alerts",
                columns: new[] { "Status", "Severity", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_SymphonyInstanceId",
                table: "Alerts",
                column: "SymphonyInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_TrackedIssueId",
                table: "Alerts",
                column: "TrackedIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorUserId_OccurredAtUtc",
                table: "AuditEvents",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundOperations_RepositoryId",
                table: "BackgroundOperations",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundOperations_Status_CreatedAtUtc",
                table: "BackgroundOperations",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundOperations_SymphonyInstanceId",
                table: "BackgroundOperations",
                column: "SymphonyInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_RepositoryId_OccurredAtUtc",
                table: "Events",
                columns: new[] { "RepositoryId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_SymphonyInstanceId_OccurredAtUtc",
                table: "Events",
                columns: new[] { "SymphonyInstanceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InstanceSnapshots_SymphonyInstanceId_CapturedAtUtc",
                table: "InstanceSnapshots",
                columns: new[] { "SymphonyInstanceId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ProjectId",
                table: "Reports",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportType_GeneratedAtUtc",
                table: "Reports",
                columns: new[] { "ReportType", "GeneratedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_RepositoryId",
                table: "Reports",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_ProjectId",
                table: "Repositories",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Provider_Owner_Name",
                table: "Repositories",
                columns: new[] { "Provider", "Owner", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunAttempts_RunId_AttemptNumber",
                table: "RunAttempts",
                columns: new[] { "RunId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Runs_RepositoryId_GitHubIssueNumber",
                table: "Runs",
                columns: new[] { "RepositoryId", "GitHubIssueNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_SymphonyInstanceId_Status_StartedAtUtc",
                table: "Runs",
                columns: new[] { "SymphonyInstanceId", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_TrackedIssueId",
                table: "Runs",
                column: "TrackedIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_SecretDescriptors_ScopeType_ScopeId_SecretType",
                table: "SecretDescriptors",
                columns: new[] { "ScopeType", "ScopeId", "SecretType" });

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_ExecutionMode",
                table: "SymphonyInstances",
                column: "ExecutionMode");

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_RepositoryId",
                table: "SymphonyInstances",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_Status_HealthStatus",
                table: "SymphonyInstances",
                columns: new[] { "Status", "HealthStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_WorkflowProfileId",
                table: "SymphonyInstances",
                column: "WorkflowProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyReleaseArtifacts_ReleaseTag_AssetName",
                table: "SymphonyReleaseArtifacts",
                columns: new[] { "ReleaseTag", "AssetName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedIssues_RepositoryId_GitHubIssueNumber",
                table: "TrackedIssues",
                columns: new[] { "RepositoryId", "GitHubIssueNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedIssues_RepositoryId_SymphonyStatus_IsBlocked",
                table: "TrackedIssues",
                columns: new[] { "RepositoryId", "SymphonyStatus", "IsBlocked" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "BackgroundOperations");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "InstanceSnapshots");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "RunAttempts");

            migrationBuilder.DropTable(
                name: "SecretDescriptors");

            migrationBuilder.DropTable(
                name: "SymphonyReleaseArtifacts");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropTable(
                name: "SymphonyInstances");

            migrationBuilder.DropTable(
                name: "TrackedIssues");

            migrationBuilder.DropTable(
                name: "WorkflowProfiles");

            migrationBuilder.DropTable(
                name: "Repositories");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
