using EggLedger.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ProtoBuf.Meta;

// protobuf-net compiles its type model at runtime via Reflection.Emit by
// default. The Blazor WASM runtime forbids Reflection.Emit, so the model
// build throws and Serializer.Deserialize fails. Disable AutoCompile so the
// reflection-based interpreter handles decode instead (no IL emit).
RuntimeTypeModel.Default.AutoCompile = false;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin: HttpClient base address is the host the app was served from. The
// rest of the UI services are shared with the Photino desktop host.
builder.Services.AddEggLedgerWeb(new Uri(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();
