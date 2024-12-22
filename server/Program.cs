using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TcpChatServer;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Program");

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile($"appsettings.json", true, true);

AppConfig options = new();
builder.Configuration.GetSection(nameof(AppConfig)).Bind(options);
builder.Services.AddSingleton<Server>();

var config = new AppConfig {
	ServerDisplayName = options.ServerDisplayName,
    ServerAddress = options.ServerAddress,
    ServerPort = options.ServerPort
};
builder.Services.AddSingleton<AppConfig>(config);

var app = builder.Build();

// Test print out config vars
var ServerDisplayName = options.ServerDisplayName;
var ServerAddress = options.ServerAddress;
var ServerPort = options.ServerPort;

Console.WriteLine("Reading settings in Program.cs");
Console.WriteLine($"Server name: {ServerDisplayName}");
Console.WriteLine($"Server address: {ServerAddress}");
Console.WriteLine($"Server port: {ServerPort}");
Console.WriteLine("================================================");

Server? ChatServer = app.Services.GetService<Server>();

Console.WriteLine("Starting ChatServer");
ChatServer?.Start();

/*
Save following as appsettings.json with appropriate settings
Must match client settings
{
	"AppConfig":
	{
		"ServerAddress": "Ryan's Super Server",
		"ServerAddress": "10.0.1.201",
		"ServerPort": 6000
	}
}
*/
