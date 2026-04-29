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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TargetResourceType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TargetResourceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TargetResourceType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    TargetResourceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ErrorDetail = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OwnerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DefaultBranchPolicy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReportType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PeriodEndUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Markdown = table.Column<string>(type: "TEXT", nullable: false),
                    Html = table.Column<string>(type: "TEXT", nullable: false),
                    PdfPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecretDescriptors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SecretType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ScopeType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RotatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecretDescriptors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SymphonyReleaseArtifacts",
                columns: table => new
                {
                    ReleaseTag = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AssetName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    DownloadedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Checksum = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymphonyReleaseArtifacts", x => new { x.ReleaseTag, x.AssetName });
                });

            migrationBuilder.CreateTable(
                name: "WorkflowProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WorkflowSource = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DefaultBranch = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CloneUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    WebUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Visibility = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    OrchestrationStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OrchestrationStatusReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
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
                name: "SymphonyInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ExecutionMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AzureResourceId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    HealthStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SymphonyVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SymphonyReleaseTag = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SymphonyArtifactSourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    SymphonyArtifactChecksum = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    GitHubCredentialSecretId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GitHubCredentialInheritanceMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OpenAiCredentialSecretId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OpenAiCredentialInheritanceMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    WorkflowPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    DataPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastStartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
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
                        name: "FK_SymphonyInstances_SecretDescriptors_GitHubCredentialSecretId",
                        column: x => x.GitHubCredentialSecretId,
                        principalTable: "SecretDescriptors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SymphonyInstances_SecretDescriptors_OpenAiCredentialSecretId",
                        column: x => x.OpenAiCredentialSecretId,
                        principalTable: "SecretDescriptors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TrackedIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitHubIssueNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LabelsJson = table.Column<string>(type: "TEXT", nullable: true),
                    Milestone = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    AssigneeLoginsJson = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    SymphonyStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LastRunStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsBlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    BlockerReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
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
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SymphonyInstanceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IssueNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SymphonyInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    HealthStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    HealthJson = table.Column<string>(type: "TEXT", nullable: true),
                    RuntimeJson = table.Column<string>(type: "TEXT", nullable: true),
                    StateJson = table.Column<string>(type: "TEXT", nullable: true),
                    ActiveIssueCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RunningSessionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryQueueCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedRunCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenInputTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    TokenOutputTotal = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SymphonyInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitHubIssueNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SymphonyRunId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenInput = table.Column<long>(type: "INTEGER", nullable: false),
                    TokenOutput = table.Column<long>(type: "INTEGER", nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    PullRequestUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RecommendedAction = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ResolutionNote = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SymphonyInstanceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GitHubIssueNumber = table.Column<int>(type: "INTEGER", nullable: true)
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
                        name: "FK_Alerts_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_SymphonyInstances_SymphonyInstanceId",
                        column: x => x.SymphonyInstanceId,
                        principalTable: "SymphonyInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RunAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExitReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ErrorDetail = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "IX_Alerts_RunId",
                table: "Alerts",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Status_Severity_CreatedAtUtc",
                table: "Alerts",
                columns: new[] { "Status", "Severity", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_SymphonyInstanceId",
                table: "Alerts",
                column: "SymphonyInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorUserId_OccurredAtUtc",
                table: "AuditEvents",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundOperations_Status_CreatedAtUtc",
                table: "BackgroundOperations",
                columns: new[] { "Status", "CreatedAtUtc" });

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
                name: "IX_Reports_ReportType_GeneratedAtUtc",
                table: "Reports",
                columns: new[] { "ReportType", "GeneratedAtUtc" });

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
                name: "IX_SecretDescriptors_ScopeType_ScopeId_SecretType",
                table: "SecretDescriptors",
                columns: new[] { "ScopeType", "ScopeId", "SecretType" });

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_ExecutionMode",
                table: "SymphonyInstances",
                column: "ExecutionMode");

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_GitHubCredentialSecretId",
                table: "SymphonyInstances",
                column: "GitHubCredentialSecretId");

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_OpenAiCredentialSecretId",
                table: "SymphonyInstances",
                column: "OpenAiCredentialSecretId");

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_RepositoryId",
                table: "SymphonyInstances",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyInstances_Status_HealthStatus",
                table: "SymphonyInstances",
                columns: new[] { "Status", "HealthStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SymphonyReleaseArtifacts_ReleaseTag",
                table: "SymphonyReleaseArtifacts",
                column: "ReleaseTag");

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
                name: "SymphonyReleaseArtifacts");

            migrationBuilder.DropTable(
                name: "TrackedIssues");

            migrationBuilder.DropTable(
                name: "WorkflowProfiles");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropTable(
                name: "SymphonyInstances");

            migrationBuilder.DropTable(
                name: "Repositories");

            migrationBuilder.DropTable(
                name: "SecretDescriptors");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
