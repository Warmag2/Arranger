using Faemiyah.Arranger.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddScoped(_ => ArrangerApi.CreateHttpClient(builder.HostEnvironment.BaseAddress));
builder.Services.AddScoped<ArrangerApi>();
builder.Services.AddScoped<CurrentUser>();

await builder.Build().RunAsync();
