using EggLedger.Domain.Api;
using EggLedger.Web;
using EggLedger.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin: HttpClient base address is the host the app was served from.
builder.Services.AddEggLedgerWeb(new Uri(builder.HostEnvironment.BaseAddress));

// The browser cannot run protobuf-net decode (Reflection.Emit is forbidden), so
// override the in-process decoder with one delegating to the sync server's decode
// endpoint (same-origin via the /api/v1 proxy).
builder.Services.AddScoped<IApiPayloadDecoder>(sp =>
    new RemoteApiPayloadDecoder(sp.GetRequiredService<HttpClient>()));

await builder.Build().RunAsync();
