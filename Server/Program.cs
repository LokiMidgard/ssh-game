// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
bool s_IsRunning = true;
Console.CancelKeyPress += Console_CancelKeyPress;

SshGame.Server.Server server = new SshGame.Server.Server();
server.Start();

while (s_IsRunning)
{
    server.Poll();
    System.Threading.Thread.Sleep(25);
}

server.Stop();

void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
{
    e.Cancel = true;
    s_IsRunning = false;
}