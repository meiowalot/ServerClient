 using System;
 using System.IO;
 using Microsoft.Extensions.Configuration;

 // See https://aka.ms/new-console-template for more information
 Console.WriteLine("Program");

 var builder = new ConfigurationBuilder()
                 .AddJsonFile($"appsettings.json", true, true);
 var config = builder.Build();

 var ServerAddress = config["ServerAddress"];
 var ServerPort = Int16.Parse(config["ServerPort"]);
 Console.WriteLine($"Server address: {ServerAddress}");
 Console.WriteLine($"Server port: {ServerPort}");

