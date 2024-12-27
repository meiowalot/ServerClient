using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TcpChatMessenger;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Program");

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile($"appsettings.json", true, true);

AppConfig options = new();
builder.Configuration.GetSection(nameof(AppConfig)).Bind(options);
builder.Services.AddSingleton<Client>();

var config = new AppConfig {
 	ServerAddress = options.ServerAddress,
	ServerPort = options.ServerPort,
	ShowDetailedOutput = options.ShowDetailedOutput
};
builder.Services.AddSingleton<AppConfig>(config);

var app = builder.Build();

// Test print out config vars
var ServerAddress = options.ServerAddress;
var ServerPort = options.ServerPort;
var ShowDetailedOutput = options.ShowDetailedOutput;

Console.WriteLine("Reading settings in Program.cs");
Console.WriteLine($"Server address: {ServerAddress}");
Console.WriteLine($"Server port: {ServerPort}");
Console.WriteLine($"Detailed output: {ShowDetailedOutput}");
Console.WriteLine("================================================");

Client? ChatClient = app.Services.GetService<Client>();

Console.WriteLine("Starting ChatClient");
ChatClient?.Start();

/*
Save following as appsettings.json with appropriate settings
Must match server settings
{
	"AppConfig":
	{
		"ServerAddress": "10.0.1.201",
		"ServerPort": 6000,
		"ShowDetailedOutput": true

	}
}
*/
