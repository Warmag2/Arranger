using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Server.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Faemiyah.Arranger.IntegrationTests;

[TestFixture]
public sealed class ArrangerCookieEventsTests
{
    private const string Password = "correct horse";

    private static readonly TimeSpan PastInterval = ArrangerCookieEvents.ValidationInterval * 2;

    private AccountService _accounts = null!;
    private ArrangerCookieEvents _events = null!;
    private ServiceProvider _services = null!;

    [SetUp]
    public void SetUp()
    {
        _services = TestServices.Create(services => services.AddAuthentication().AddCookie());
        _accounts = _services.GetRequiredService<AccountService>();
        _events = new ArrangerCookieEvents(_accounts, TimeProvider.System);
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        await _services.DisposeAsync();
    }

    [Test]
    public async Task ValidatePrincipal_AfterInterval_RejectsChangedStamp()
    {
        var registered = (await _accounts.RegisterAsync("Warma", Password, null)).User!;
        await _accounts.ChangePasswordAsync(registered.UniqueId, Password, "new password");
        var context = CreateContext(registered, PastInterval);

        await _events.ValidatePrincipal(context);

        AssertSignedOut(context);
    }

    [Test]
    public async Task ValidatePrincipal_AfterInterval_RejectsUnknownUser()
    {
        var context = CreateContext(new User { UniqueId = Guid.NewGuid(), UserName = "Ghost" }, PastInterval);

        await _events.ValidatePrincipal(context);

        AssertSignedOut(context);
    }

    [Test]
    public async Task ValidatePrincipal_AfterInterval_RenewsValidStamp()
    {
        var registered = (await _accounts.RegisterAsync("Warma", Password, null)).User!;
        var context = CreateContext(registered, PastInterval);

        await _events.ValidatePrincipal(context);

        context.Principal.Should().NotBeNull();
        context.ShouldRenew.Should().BeTrue();
    }

    [Test]
    public async Task ValidatePrincipal_WithinInterval_SkipsCheck()
    {
        var registered = (await _accounts.RegisterAsync("Warma", Password, null)).User!;
        await _accounts.ChangePasswordAsync(registered.UniqueId, Password, "new password");
        var context = CreateContext(registered, TimeSpan.FromMinutes(1));

        await _events.ValidatePrincipal(context);

        context.Principal.Should().NotBeNull();
        context.ShouldRenew.Should().BeFalse();
    }

    private static void AssertSignedOut(CookieValidatePrincipalContext context)
    {
        context.Principal.Should().BeNull();
        context.HttpContext.Response.Headers.SetCookie.ToString().Should().Contain("expires=Thu, 01 Jan 1970 00:00:00 GMT");
    }

    private CookieValidatePrincipalContext CreateContext(User user, TimeSpan age)
    {
        var http = new DefaultHttpContext { RequestServices = _services };
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var properties = new AuthenticationProperties { IssuedUtc = TimeProvider.System.GetUtcNow() - age };
        var ticket = new AuthenticationTicket(AccountService.CreatePrincipal(user), properties, scheme.Name);
        return new CookieValidatePrincipalContext(http, scheme, new CookieAuthenticationOptions(), ticket);
    }
}
