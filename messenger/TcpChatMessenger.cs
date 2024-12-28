// Filename:  TcpChatMessenger.cs        
// Author:    Benjamin N. Summerton <define-private-public>        
// License:   Unlicense (http://unlicense.org/)        

using Microsoft.Extensions.Configuration;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace TcpChatMessenger;

class Client
{
	// Viewer
	private readonly bool _disconnectRequested = false;
	private readonly IConfiguration _configuration;

	private AppConfig _config;

	// Connection objects
	public string? ServerAddress;
	public int? Port;

	// Chat Username
	public string? Name;

	// Show detailed/debugging info
	private bool ShowDetailedOutput;

	private TcpClient? _client;

	public bool Running { get; private set; }

	// Buffer & messaging
	public readonly int BufferSize = 2 * 1024;  // 2KB
	private NetworkStream? _msgStream = null;
	private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();

	public Client(IConfiguration configuration, AppConfig config)
	{
		if (ShowDetailedOutput)
			Console.WriteLine("Chat messenger ctor, DI config");
		_configuration = configuration;

		if (ShowDetailedOutput)
			Console.WriteLine($"DI setting config with {config.ServerPort}");
		_config = config;
	}

	public void Connect()
	{
		if (ShowDetailedOutput)
		{
			Console.WriteLine("Connect()");
		}
		String host = _config.ServerAddress;
		int port = _config.ServerPort;
		if (ShowDetailedOutput)
		{
			Console.WriteLine($"Read config address {host}");
			Console.WriteLine($"Read config port    {port}");
		}

		Console.WriteLine("Connecting to client");
		_client?.Connect(host, port);       // Will resolve DNS for us; blocks
		EndPoint endPoint = _client.Client.RemoteEndPoint;

		// Make sure we're connected
		if (_client.Connected)
		{
			// Got in!
			if (ShowDetailedOutput)
			{
				Console.WriteLine("Connected to the server at endpoint {0}.", endPoint);
			}

			// Tell them that we're a messenger
			if (ShowDetailedOutput)
			{
				Console.WriteLine("Getting stream");
			}
			_msgStream = _client.GetStream();

			if (ShowDetailedOutput)
				Console.WriteLine($"Name is {Name}");

			string msg = $"name:{Name}";
			if (ShowDetailedOutput)
				Console.WriteLine($"Sending message >{msg}<");
			byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);

			if (ShowDetailedOutput)
				Console.WriteLine("Writing stream");
			_msgStream.Write(msgBuffer, 0, msgBuffer.Length);   // Blocks

			// If we're still connected after sending our name, that means the server accepts us
			if (ShowDetailedOutput)
				Console.WriteLine("Still connected after sending our name, server accepts us");
			if (!IsDisconnected(_client))
				Running = true;
			else
			{
				// Name was probably taken...
				CleanupNetworkResources();
				if (ShowDetailedOutput)
					Console.WriteLine("The server rejected us; \"{0}\" is probably in use.", Name);
			}
		}
		else
		{
			CleanupNetworkResources();
			if (ShowDetailedOutput)
				Console.WriteLine("Wasn't able to connect to the server at {0}.", endPoint);
		}
	}

	public void HandleMessages() 
	{
		bool wasRunning = Running;
		if (ShowDetailedOutput)
		{
			Console.WriteLine("HandleMessages()");
		}

		Task.Run(()=>{
			SendMessages(); 
		});

		ListenForMessages(); 
	}

	public void SendMessages()
	{
		bool wasRunning = Running;

		if (ShowDetailedOutput)
		{
			Console.WriteLine("SendMessages(), while loop:");
			Console.WriteLine("Entering SendMessages while loop");
		}
		string strKeysPressed = "", msg = "";
		while (Running)
		{
			// get user input w/o blocking
			if (Console.KeyAvailable)
			{
				ConsoleKeyInfo key = Console.ReadKey(true);
				if (key.KeyChar == '\r')
				{
					Console.WriteLine();
					msg = strKeysPressed;
					strKeysPressed = "";
				}
				else	
				{
					Console.Write(key.KeyChar);
					strKeysPressed += key.KeyChar;
				}
			}
			// end user input

			// Quit or send a message
			if (msg.ToLower() is "quit" or "exit")
			{
				// User wants to quit
				if (ShowDetailedOutput)
					Console.WriteLine("Disconnecting...");
				Running = false;
			}
			else if (msg == "listusers")
			{
				if (ShowDetailedOutput)
					Console.WriteLine($"listusers");
				byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);
				_msgStream?.Write(msgBuffer, 0, msgBuffer.Length);   // Blocks
				msg = "";
			}
			else if (msg != string.Empty)
			{
				// Send the message
				if (ShowDetailedOutput)
					Console.WriteLine($"Sending message >{msg}<");
				byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);
				_msgStream?.Write(msgBuffer, 0, msgBuffer.Length);   // Blocks
				msg = "";
			}
			if(!_queue.IsEmpty)
			{
				if(_queue.TryDequeue(out string? str))
				{
					// Print response
					Console.WriteLine(str);
					Console.Write("> ");
				}
				else 
				{
					if (ShowDetailedOutput)
					{
						Console.WriteLine("TryDequeue failed");
					}
				}
			}

			// Use less CPU
			Thread.Sleep(10);

			// Check the server didn't disconnect us
			if (IsDisconnected(_client))
			{
				Running = false;
				if (ShowDetailedOutput)
					Console.WriteLine("Server has disconnected from us.\n:[");
			}
		}

		CleanupNetworkResources();
		if (wasRunning)
			Console.WriteLine("Disconnected.");

		if (ShowDetailedOutput)
			Console.WriteLine($"Leaving SendMessages()");
	}

	// Cleans any leftover network resources
	private void CleanupNetworkResources()
	{
		_msgStream?.Close();
		_msgStream = null;
		_client.Close();
	}

	// Checks if a socket has disconnected
	// Adapted from -- http://stackoverflow.com/questions/722240/instantly-detect-client-disconnection-from-server-socket
	private static bool IsDisconnected(TcpClient client)
	{
		try
		{
			Socket s = client.Client;
			return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
		}
		catch(ObjectDisposedException)
		{
			// We got a socket error, assume it's disconnected
			return true;
		}
		catch(SocketException)
		{
			// We got a socket error, assume it's disconnected
			return true;
		}
	}

	// connects to the chat server
	public void ConnectViewer()
	{
		if (ShowDetailedOutput)
		{
			Console.WriteLine("ConnectViewer()");
		}
		return;

		// Now try to connect
		// Send them the message that we're a viewer
		_msgStream = _client?.GetStream();

		string msg = "viewer";
		byte[] msgBufferViewer = Encoding.UTF8.GetBytes(msg);
		if (ShowDetailedOutput)
		{
			Console.WriteLine($"Sending message >{msg}<");
		}
		_msgStream?.Write(msgBufferViewer, 0, msgBufferViewer.Length);     // Blocks
		return;
	}

	// Main loop, listens and prints messages from the server
	public void ListenForMessages()
	{
		bool wasRunning = Running;

		if (ShowDetailedOutput)
		{
			Console.WriteLine("ListenForMessages(), while loop:");
		}
		if (_client is null)
			if (ShowDetailedOutput)
			{
				Console.WriteLine("LFM: client is null");
			}
		// Listen for messages
		while (Running)
		{
			// Do we have a new message?
			int messageLength = _client.Available;
			if (messageLength > 0)
			{
				if (ShowDetailedOutput)
					Console.WriteLine("New incoming message of {0} bytes", messageLength);

				// Read the whole message
				byte[] msgBuffer = new byte[messageLength];
				_msgStream?.Read(msgBuffer, 0, messageLength);   // Blocks

				// An alternative way of reading
				//int bytesRead = 0;
				//while (bytesRead < messageLength)
				//{
				//    bytesRead += _msgStream.Read(_msgBuffer,
				//                                 bytesRead,
				//                                 _msgBuffer.Length - bytesRead);
				//    Thread.Sleep(1);    // Use less CPU
				//}

				// Decode it and print it
				string msg = Encoding.UTF8.GetString(msgBuffer);
				_queue.Enqueue(msg);
			}

			// Use less CPU
			Thread.Sleep(10);

			// Check the server didn't disconnect us
			if (IsDisconnected(_client))
			{
				Running = false;
				Console.WriteLine("Server has disconnected from us.\n:[");
			}

			// Check that a cancel has been requested by the user
			Running &= !_disconnectRequested;
		}

		// Cleanup
		CleanupNetworkResources();
		if (wasRunning)
			Console.WriteLine("Disconnected.");

		if (ShowDetailedOutput)
			Console.WriteLine($"Leaving ListenForMessages()");
	}

	public void Start()
	{
		ShowDetailedOutput = _config.ShowDetailedOutput;

		if (ShowDetailedOutput)
		{
			Console.WriteLine("Start()");
		}

		// Get a name
		Console.Write("Enter a name to use: ");
		Name = Console.ReadLine();

		_client = new TcpClient
		{
			SendBufferSize = BufferSize,
			ReceiveBufferSize = BufferSize
		};
				Running = false;

		// connect and send messages
		if (ShowDetailedOutput)
		{
			Console.WriteLine("messenger.Connect");
		}
		Connect();

		if (ShowDetailedOutput)
		{
			Console.WriteLine("messenger.ConnectViewer");
		}
		ConnectViewer();

		if (ShowDetailedOutput)
		{
			Console.WriteLine("messenger.HandleMessages");
		}
		HandleMessages();
	}

} // class

