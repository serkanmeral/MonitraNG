using MngDocument.Infrastructure.Services;

var outputDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));

Directory.CreateDirectory(outputDir);

var xlsxPath = Path.Combine(outputDir, "MonitraNG-Demo-Elektronik-Tablo.xlsx");
var pptxPath = Path.Combine(outputDir, "MonitraNG-Demo-Sunum.pptx");

var xlsx = CollaboraDemoXlsxFactory.CreateDemo();
var pptx = CollaboraDemoPptxFactory.CreateDemo();

await File.WriteAllBytesAsync(xlsxPath, xlsx);
await File.WriteAllBytesAsync(pptxPath, pptx);

Console.WriteLine($"OK: {xlsxPath} ({xlsx.Length:N0} bytes)");
Console.WriteLine($"OK: {pptxPath} ({pptx.Length:N0} bytes)");
