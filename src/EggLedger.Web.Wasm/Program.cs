using EggLedger.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin: HttpClient base address is the host the app was served from. The
// rest of the UI services are shared with the Photino desktop host.
builder.Services.AddEggLedgerWeb(new Uri(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();
