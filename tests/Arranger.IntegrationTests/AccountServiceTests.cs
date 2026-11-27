using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Server.Authentication;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Faemiyah.Repository.Interfaces;

namespace Faemiyah.Arranger.IntegrationTests;

[TestFixture]
public class AccountServiceTests
{
    private const string Password = "correct horse";

    private ServiceProvider _services = null!;
    private AccountService _accounts = null!;
    private IEntityRepositoryWithUuid<long, User> _users = null!;

    [SetUp]
    public void SetUp()
    {
        _services = TestServices.Create();
        _accounts = _services.GetRequiredService<AccountService>();
        _users = _services.GetRequiredService<IEntityRepositoryWithUuid<long, User>>();
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        await _services.DisposeAsync();
    }

    [Test]
    public async Task ChangePassword_Fails_WhenCurrentPasswordIsWrong()
    {
        var registered = (await _accounts.RegisterAsync("Warma", Password, null)).User!;

        var result = await _accounts.ChangePasswordAsync(registered.UniqueId, "wrong password", "new password");

        result.Status.Should().Be(AccountStatus.WrongPassword);
        (await _accounts.IsSecurityStampValidAsync(registered.UniqueId, registered.SecurityStamp)).Should().BeTrue();
    }

    [Test]
    public async Task ChangePassword_KeepsNormalizedUserName()
    {
        var registered = (await _accounts.RegisterAsync("Warma", Password, null)).User!;

        await _accounts.ChangePasswordAsync(registered.UniqueId, Password, "new password");

        var stored = await _users.GetAsync(registered.UniqueId);
        stored!.NormalizedUserName.Should().Be("WARMA");
    }

    [Test]
    public async Task ChangePassword_ReplacesPassword()
    {
        var registered = (await _accounts.RegisterAsync("Warma", Password, null)).User!;

        var result = await _accounts.ChangePasswordAsync(registered.UniqueId, Password, "new password");

        result.Status.Should().Be(AccountStatus.Success);
        (await _accounts.LoginAsync("Warma", Password, null)).Should().BeNull();
        (await _accounts.LoginAsync("Warma", "new password", null)).Should().NotBeNull();
    }

    [Test]
    public async Task ChangePassword_ReplacesSecurityStamp()
    {
        var registered = (await _accounts.RegisterAsync("Warma", Password, null)).User!;

        var result = await _accounts.ChangePasswordAsync(registered.UniqueId, Password, "new password");

        (await _accounts.IsSecurityStampValidAsync(registered.UniqueId, registered.SecurityStamp)).Should().BeFalse();
        (await _accounts.IsSecurityStampValidAsync(registered.UniqueId, result.User!.SecurityStamp)).Should().BeTrue();
    }

    [Test]
    public async Task ChangePassword_ReportsUnknownUser()
    {
        var result = await _accounts.ChangePasswordAsync(Guid.NewGuid(), Password, "new password");

        result.Status.Should().Be(AccountStatus.UserNotFound);
    }

    [Test]
    public async Task IsSecurityStampValid_IsFalse_ForUnknownUser()
    {
        (await _accounts.IsSecurityStampValidAsync(Guid.NewGuid(), Guid.NewGuid())).Should().BeFalse();
    }

    [TestCase("Warma")]
    [TestCase("warma")]
    [TestCase("WARMA")]
    public async Task Login_IgnoresUserNameCase(string userName)
    {
        await _accounts.RegisterAsync("Warma", Password, null);

        var user = await _accounts.LoginAsync(userName, Password, IPAddress.Loopback);

        user!.UserName.Should().Be("Warma");
    }

    [TestCase("Warma", "wrong password")]
    [TestCase("Nobody", Password)]
    [TestCase("not valid!", Password)]
    public async Task Login_ReturnsNull_ForWrongCredentials(string userName, string password)
    {
        await _accounts.RegisterAsync("Warma", Password, null);

        var user = await _accounts.LoginAsync(userName, password, IPAddress.Loopback);

        user.Should().BeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Mars/Olympus_Mons")]
    public async Task Register_FallsBackToUtc_WhenTimeZoneIsUnknown(string? timeZoneId)
    {
        var result = await _accounts.RegisterAsync("Warma", Password, timeZoneId);

        result.User!.TimeZoneId.Should().Be("UTC");
    }

    [Test]
    public async Task Register_LetsOnlyOneOfConcurrentRegistrationsOfTheSameNameSucceed()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => _accounts.RegisterAsync(i % 2 == 0 ? "Warma" : "WARMA", Password, null)));

        results.Count(r => r.Status == AccountStatus.Success).Should().Be(1);
        results.Count(r => r.Status == AccountStatus.UserNameTaken).Should().Be(7);
    }

    [TestCase("ab")]
    [TestCase("has space")]
    [TestCase("ääkkönen")]
    public async Task Register_RejectsInvalidUserName(string userName)
    {
        var result = await _accounts.RegisterAsync(userName, Password, null);

        result.Status.Should().Be(AccountStatus.InvalidUserName);
    }

    [Test]
    public async Task Register_RejectsTooShortPassword()
    {
        var result = await _accounts.RegisterAsync("Warma", "short", null);

        result.Status.Should().Be(AccountStatus.InvalidPassword);
    }

    [Test]
    public async Task Register_RejectsUserName_ThatDiffersOnlyInCase()
    {
        await _accounts.RegisterAsync("Warma", Password, null);

        var result = await _accounts.RegisterAsync("wARMA", Password, null);

        result.Status.Should().Be(AccountStatus.UserNameTaken);
        result.User.Should().BeNull();
    }

    [Test]
    public async Task Register_StoresUserNameCasingAndNormalizedName()
    {
        var result = await _accounts.RegisterAsync("Warma", Password, "Europe/Helsinki");

        result.Status.Should().Be(AccountStatus.Success);
        var stored = await _users.GetAsync(result.User!.UniqueId);
        stored!.UserName.Should().Be("Warma");
        stored.NormalizedUserName.Should().Be("WARMA");
        stored.TimeZoneId.Should().Be("Europe/Helsinki");
        stored.DefaultCurrency.Should().Be("EUR");
        stored.PasswordHash.Should().NotContain(Password);
    }
}
