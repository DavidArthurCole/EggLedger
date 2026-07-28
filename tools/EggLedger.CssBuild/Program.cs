using System.Text.RegularExpressions;
using EggIdentity.Styles;
using EggLedger.CssBuild;
using MonorailCss;
using MonorailCss.Parser.SourceCss;

if (args.Length < 1) {
    Console.Error.WriteLine("Usage: EggLedger.CssBuild <EggLedger.Web project directory>");
    return 1;
}

var webProjectDir = Path.GetFullPath(args[0]);
if (!Directory.Exists(webProjectDir)) {
    Console.Error.WriteLine($"Web project directory not found: {webProjectDir}");
    return 1;
}

var cssSourcePath = Path.Combine(webProjectDir, "Styles", "app.v4.css");
if (!File.Exists(cssSourcePath)) {
    Console.Error.WriteLine($"CSS source file not found: {cssSourcePath}");
    return 1;
}

var outputPath = Path.Combine(webProjectDir, "wwwroot", "tailwind.css");
var desktopHtmlPath = Path.GetFullPath(Path.Combine(webProjectDir, "..", "EggLedger.Desktop", "wwwroot", "desktop.html"));
var serverProjectDir = Path.GetFullPath(Path.Combine(webProjectDir, "..", "EggLedger.Web.Server"));

var contentFiles = new List<string>();
contentFiles.AddRange(Directory.EnumerateFiles(webProjectDir, "*.razor", SearchOption.AllDirectories));
if (File.Exists(desktopHtmlPath)) {
    contentFiles.Add(desktopHtmlPath);
}
if (Directory.Exists(serverProjectDir)) {
    contentFiles.AddRange(Directory.EnumerateFiles(serverProjectDir, "*.razor", SearchOption.AllDirectories));
}

Console.WriteLine($"Scanning {contentFiles.Count} content files for utility/component class tokens...");
var candidates = ContentScanner.Scan(contentFiles);
Console.WriteLine($"Found {candidates.Count} distinct candidate tokens.");

var processor = new CssSourceProcessor(message => Console.WriteLine($"[monorail] {message}"));
var sourceResult = processor.ProcessFile(cssSourcePath, null);

var mergedApplies = ComponentClasses.All.SetItems(sourceResult.Settings.Applies);
var settings = sourceResult.Settings with { Applies = mergedApplies };

var framework = new CssFramework(settings);
var compiledCss = framework.Process(candidates);

var strippedRawCss = Regex.Replace(sourceResult.RawCss, "@apply[^;]*;", string.Empty);

var finalCss = compiledCss + "\n" + strippedRawCss;

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, finalCss);

Console.WriteLine($"Wrote {finalCss.Length} chars to {outputPath}");
return 0;
