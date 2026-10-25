using System;
using System.IO;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Data.Extensions;
using Faemiyah.Arranger.Server.Authentication;
using Faemiyah.Arranger.Server.Components;
using Faemiyah.Arranger.Server.Endpoints;
using Faemiyah.Arranger.Server.Hosting;
using Faemiyah.Arranger.Server.Realtime;
using Faemiyah.Arranger.Server.Users;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddArrangerLogging();
builder.AddArrangerOrleans();
builder.Services.ConfigureArrangerJson();
builder.Services.AddArrangerData(builder.Configuration);
builder.Services.AddArrangerProxyAndRateLimiting(builder.Configuration);
builder.Services.AddArrangerRealtime();
builder.Services.AddSingleton<TimeZoneCatalog>();
builder.Services.AddProblemDetails();

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.Configure<RegistrationOptions>(builder.Configuration.GetSection(RegistrationOptions.SectionName));
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddScoped<ArrangerCookieEvents>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Arranger.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.EventsType = typeof(ArrangerCookieEvents);
    });
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Arranger");
var dataProtectionKeysPath = builder.Configuration["DataProtectionKeysPath"];
if (!string.IsNullOrEmpty(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

var app = builder.Build();

// First, so that everything after sees the real client IP and scheme from the reverse proxy.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseWhen(
    context => !ArrangerCookieEvents.IsApiRequest(context.Request),
    pages => pages.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));

app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/healthz");
app.MapAccountEndpoints();
app.MapUserEndpoints();
app.MapArrangementEndpoints();
app.MapArrangerHubs();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Faemiyah.Arranger.Client._Imports).Assembly);

await app.RunAsync();
