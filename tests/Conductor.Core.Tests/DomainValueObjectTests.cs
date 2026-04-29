using Conductor.Core.Domain.Ids;
using Conductor.Core.Domain.Releases;
using Conductor.Core.Domain.Repositories;
using Conductor.Core.Domain.SymphonyReleases;
using Conductor.Core.Domain.Time;

namespace Conductor.Core.Tests;

public sealed class DomainValueObjectTests
{
    [Fact]
    public void EntityIds_Reject_Empty_Guid_Values()
    {
        Assert.Throws<ArgumentException>(() => new BackgroundOperationId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ProjectId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ReportId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new RepositoryId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new RunId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new SecretId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new SymphonyInstanceId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new WorkflowProfileId(Guid.Empty));
    }

    [Fact]
    public void EntityIds_Parse_And_Format_Stable_Guid_Values()
    {
        var guid = Guid.Parse("6d571ce1-f2f8-46d9-b12d-9ef8cf773d71");
        ProjectId id = ProjectId.Parse(guid.ToString("D"));

        Assert.Equal(guid, id.Value);
        Assert.Equal(guid.ToString("D"), id.ToString());
        Assert.True(ProjectId.TryParse(guid.ToString("D"), out ProjectId parsed));
        Assert.Equal(id, parsed);
        Assert.False(ProjectId.TryParse(Guid.Empty.ToString("D"), out _));
    }

    [Fact]
    public void GitHubRepositoryFullName_Parses_Owner_And_Repository_Name()
    {
        GitHubRepositoryFullName fullName = GitHubRepositoryFullName.Parse("  ReleasedGroup / TheConductor  ");

        Assert.Equal("ReleasedGroup", fullName.Owner);
        Assert.Equal("TheConductor", fullName.Name);
        Assert.Equal("ReleasedGroup/TheConductor", fullName.Value);
        Assert.Equal(fullName.Value, fullName.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("owner/repo/extra")]
    [InlineData("owner / repo with spaces")]
    public void GitHubRepositoryFullName_Rejects_Invalid_Values(string value)
    {
        Assert.False(GitHubRepositoryFullName.TryParse(value, out _));
        Assert.ThrowsAny<ArgumentException>(() => GitHubRepositoryFullName.Parse(value));
    }

    [Fact]
    public void ReleaseSelector_Uses_ReleaseTag_For_Pinned_Releases()
    {
        ReleaseSelector selector = ReleaseSelector.PinnedTag("  v1.2.3  ");

        Assert.False(selector.IsLatest);
        Assert.NotNull(selector.Tag);
        Assert.Equal("v1.2.3", selector.Tag!.Value);
        Assert.Equal("v1.2.3", selector.ToString());
    }

    [Fact]
    public void SymphonyReleaseArtifact_Stores_A_Typed_Release_Tag()
    {
        var artifact = new SymphonyReleaseArtifact(
            new ReleaseTag("v2.0.0"),
            "symphony-linux-x64.zip",
            new Uri("https://github.com/ReleasedGroup/Symphony/releases/download/v2.0.0/symphony-linux-x64.zip"),
            DateTimeOffset.Parse("2026-04-29T00:00:00Z"),
            checksum: "  sha256:abc123  ");

        Assert.Equal(new ReleaseTag("v2.0.0"), artifact.ReleaseTag);
        Assert.Equal("sha256:abc123", artifact.Checksum);
    }

    [Fact]
    public void UtcDateTimeRange_Requires_Utc_Ordered_Bounds()
    {
        var start = DateTimeOffset.Parse("2026-04-29T00:00:00Z");
        var end = DateTimeOffset.Parse("2026-04-29T02:00:00Z");
        var range = new UtcDateTimeRange(start, end);

        Assert.Equal(TimeSpan.FromHours(2), range.Duration);
        Assert.True(range.Contains(start.AddHours(1)));
        Assert.True(range.Overlaps(new UtcDateTimeRange(start.AddHours(1), end.AddHours(1))));
        Assert.Throws<ArgumentException>(() => new UtcDateTimeRange(end, start));
        Assert.Throws<ArgumentException>(() => range.Contains(new DateTimeOffset(2026, 4, 29, 1, 0, 0, TimeSpan.FromHours(10))));
    }
}
