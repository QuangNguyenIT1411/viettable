using VietTable.CloudHost.Auth;
using Xunit;

public sealed class StaffCookieValidationTests
{
    [Theory]
    [InlineData(false, "current", "current")]
    [InlineData(true, "current", null)]
    [InlineData(false, null, null)]
    public async Task Independent_checks_start_together_and_fail_closed(bool revoked, string? version, string? expected)
    {
        var fixture = new HeldValidator();
        IStaffCookieValidator validator = fixture;
        var validation = validator.ValidateVersionAsync(1, "CHEF", 3, "opaque-test-hash", CancellationToken.None);
        Assert.True(fixture.RevocationStarted);
        Assert.True(fixture.VersionStarted);
        Assert.False(validation.IsCompleted);
        fixture.Revoked.SetResult(revoked);
        Assert.False(validation.IsCompleted);
        fixture.Version.SetResult(version);
        Assert.Equal(expected, await validation);
    }

    [Fact]
    public async Task Failed_live_check_is_not_treated_as_authenticated()
    {
        var fixture = new HeldValidator();
        IStaffCookieValidator validator = fixture;
        var validation = validator.ValidateVersionAsync(1, "CHEF", 3, "opaque-test-hash", CancellationToken.None);
        fixture.Revoked.SetResult(false);
        fixture.Version.SetException(new InvalidOperationException("test boundary unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => validation);
    }

    private sealed class HeldValidator : IStaffCookieValidator
    {
        public TaskCompletionSource<bool> Revoked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string?> Version { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool RevocationStarted { get; private set; }
        public bool VersionStarted { get; private set; }
        public Task<bool> IsRevokedAsync(string hash, CancellationToken ct)
        { RevocationStarted = true; return Revoked.Task; }
        public Task<string?> VersionAsync(int id, string role, int branch, CancellationToken ct)
        { VersionStarted = true; return Version.Task; }
        public Task RevokeAsync(string hash, DateTimeOffset expiry, CancellationToken ct) => Task.CompletedTask;
    }
}
