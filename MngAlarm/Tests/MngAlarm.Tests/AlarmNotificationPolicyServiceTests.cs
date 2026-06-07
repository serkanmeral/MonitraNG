using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Services;

namespace MngAlarm.Tests;

public sealed class AlarmNotificationPolicyServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsPolicyFields()
    {
        AlarmNotificationPolicyDocument? inserted = null;
        var repo = new FakePolicyRepository(doc => inserted = doc);
        var domain = new FakeDomainAccessor("dom-id", "odak");
        var sut = new AlarmNotificationPolicyService(domain, repo);

        await sut.CreateAsync(new CreateAlarmNotificationPolicyRequest
        {
            Name = "Raised notify",
            EventType = AlarmNotificationEventTypes.Raised,
            Channels = [AlarmNotificationChannels.InApp],
            RecipientPersonIds = ["6a0f8fd13d6ba5d774ee37c7"],
            Settings = new AlarmNotificationPolicySettingsDto { PushToast = true, ToastSeverity = "warning" },
            Priority = 10,
        });

        Assert.NotNull(inserted);
        Assert.Equal("odak", inserted!.DomainName);
        Assert.Equal(AlarmNotificationEventTypes.Raised, inserted.EventType);
        Assert.Single(inserted.RecipientPersonIds);
        Assert.True(inserted.Settings?.PushToast);
        Assert.Equal("warning", inserted.Settings?.ToastSeverity);
    }

    [Fact]
    public async Task CreateAsync_EmailChannelRequiresTemplateKey()
    {
        var sut = new AlarmNotificationPolicyService(
            new FakeDomainAccessor("dom-id", "odak"),
            new FakePolicyRepository(_ => { }));

        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateAsync(new CreateAlarmNotificationPolicyRequest
        {
            Name = "Mail only",
            EventType = AlarmNotificationEventTypes.Raised,
            Channels = [AlarmNotificationChannels.Email],
            RecipientPersonIds = ["user-1"],
        }));
    }

    private sealed class FakeDomainAccessor(string domainId, string domainName) : IAlarmDomainAccessor
    {
        public AlarmDomainContext GetRequiredDomain() => new(domainId, domainName);
    }

    private sealed class FakePolicyRepository(Action<AlarmNotificationPolicyDocument> onInsert) : IAlarmNotificationPolicyRepository
    {
        public Task InsertAsync(AlarmNotificationPolicyDocument policy, CancellationToken cancellationToken = default)
        {
            onInsert(policy);
            return Task.CompletedTask;
        }

        public Task<AlarmNotificationPolicyDocument?> GetByIdAsync(string domainName, string policyId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmNotificationPolicyDocument?>(null);

        public Task UpdateAsync(AlarmNotificationPolicyDocument policy, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string domainName, string policyId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AlarmNotificationPolicyDocument>> ListAsync(
            string domainName,
            bool? isActive,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmNotificationPolicyDocument>>([]);
    }
}
