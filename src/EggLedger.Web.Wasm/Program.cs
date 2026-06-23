using EggLedger.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ProtoBuf.Meta;

// protobuf-net compiles serializers at runtime via System.Reflection.Emit by
// default. The Blazor WASM runtime forbids Reflection.Emit, so a Deserialize
// throws Arg_TargetInvocationException. Force the reflection-based interpreter
// path (no IL emit) so protobuf decode works in the browser.
RuntimeTypeModel.Default.AutoCompile = false;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin: HttpClient base address is the host the app was served from. The
// rest of the UI services are shared with the Photino desktop host.
builder.Services.AddEggLedgerWeb(new Uri(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();
