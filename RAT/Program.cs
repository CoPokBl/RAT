using GeneralPurposeLib;
using RAT;
using LogLevel = GeneralPurposeLib.LogLevel;

Console.WriteLine("Starting...");
Logger.Init(LogLevel.Debug);
// Create cancellation token source
CancellationTokenSource cts = new();

Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

// Create cancellation token
CancellationToken ct = cts.Token;
RatHandler.ExecuteAsync(ct).Wait(ct);

// IHost host = Host.CreateDefaultBuilder(args)
//     .ConfigureServices(services => { services.AddHostedService<Worker>(); })
//     .Build();
//
// await host.RunAsync();