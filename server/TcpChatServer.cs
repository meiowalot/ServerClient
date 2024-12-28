// Filename:  TcpChatServer.cs        
// Author:    Benjamin N. Summerton <define-private-public>        
// License:   Unlicense (http://unlicense.org/)        

using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TcpChatServer;

class Server
{
	// What listens in
	private TcpListener? _listener;

	// types of clients connected
	private readonly List<TcpClient> _viewers = [];
	private readonly List<TcpClient> _messengers = [];

	private readonly IConfiguration _configuration;

	private readonly AppConfig _config;

	// Names that are taken by other messengers
	// N/S I love this structure
	// Create class to contain ref to TcpClient and user name?
	private readonly Dictionary<TcpClient, string> _names = [];

	// Messages that need to be sent
	private readonly Queue<string> _messageQueue = new();

	// Extra fun data
	public string? ChatName;
	public string? ServerAddress;
	public int Port;
	public bool Running { get; private set; }

	// Buffer
	public readonly int BufferSize = 2 * 1024;  // 2KB

	private bool ShowDetailedOutput;

	// Make a new TCP chat server, with our provided name
	//public TcpChatServer(string chatName, int port)
	public Server(IConfiguration configuration, AppConfig config)
	{
		if (ShowDetailedOutput)
		{
			Console.WriteLine("Server:ctor: Starting up");
			Console.WriteLine("Chat messenger ctor, DI config");
		}
		_configuration = configuration;
		_config = config;
	}

	// If the server is running, this will shut down the server
	public void Shutdown()
	{
		Running = false;
		if (ShowDetailedOutput)
		{
			Console.WriteLine("Shutting down server");
		}
	}

	// Start running the server.  Will stop when `Shutdown()` has been called
	public void Run()
	{
		// Some info
		if (ShowDetailedOutput)
		{
			Console.WriteLine("Starting the \"{0}\" TCP Chat Server from {1} on port {2}.", ChatName, ServerAddress, Port);
			Console.WriteLine("Press Ctrl-C to shut down the server at any time.");
		}

		//Console.TreatControlCAsInput = true;
		Console.CancelKeyPress += delegate(object? sender, ConsoleCancelEventArgs e) {
			Console.WriteLine("Ctrl-c delegate");
			this?.Shutdown();
			e.Cancel = true;
		};

		// Make the server run
		_listener?.Start();           // No backlog
		Running = true;

		// Main server loop
		while (Running)
		{
			// Check for new clients
			if (_listener is not null)
				if (_listener.Pending())
					HandleNewConnection();

			// Do the rest
			CheckForDisconnects();
			CheckForNewMessages();
			SendMessages();

			// Use less CPU
			Thread.Sleep(10);
		}

		// Stop the server, and clean up any connected clients
		foreach (TcpClient v in _viewers)
			CleanupClient(v);
		foreach (TcpClient m in _messengers)
			CleanupClient(m);
		_listener?.Stop();

		// Some info
		Console.WriteLine("Server is shut down.");
	}

	private async void HandleNewConnection()
	{
		bool good = false;

		if (_listener is null)
			return;

		TcpClient newClient = _listener.AcceptTcpClient();
		NetworkStream netStream = newClient.GetStream();

		// Modify the default buffer sizes
		newClient.SendBufferSize = BufferSize;
		newClient.ReceiveBufferSize = BufferSize;

		// Print some info
		EndPoint? endPoint = newClient.Client.RemoteEndPoint;

		if (ShowDetailedOutput)
		{
			Console.WriteLine("====================================");
			Console.WriteLine("Handling a new client from {0}...", endPoint);
		}

		// Let them identify themselves
		byte[] msgBuffer = new byte[BufferSize];

		// Blocks
		int bytesRead = await netStream.ReadAsync(msgBuffer, 0, msgBuffer.Length);
		if (bytesRead > 0)
		{
			string msg = Encoding.UTF8.GetString(msgBuffer, 0, bytesRead);

			if (ShowDetailedOutput)
			{
				Console.WriteLine("Read bytes");
				Console.WriteLine($"msg: >{msg}<");
			}

			if (endPoint is null)
			{
				// ???
				return;
			}

			IPAddress? ip = IPAddress.Parse(((IPEndPoint)endPoint).Address.ToString());
			var port = IPAddress.Parse(((IPEndPoint)endPoint).Port.ToString());
			string addr = $"{ip}:{port}";

			if (msg == "viewer")
			{
				// They just want to watch
				if (ShowDetailedOutput)
				{
					Console.WriteLine($"=======Adding viewer @ {addr}===========");
				}
				good = true;
				_viewers.Add(newClient);
				ShowViewers();

				if (ShowDetailedOutput)
				{
					Console.WriteLine("{0} is a Viewer.", endPoint);
				}

				// Send them a "hello message"
				if (ShowDetailedOutput)
				{
					msg = String.Format("Welcome to the \"{0}\" Chat Server!", ChatName);
				}
				msgBuffer = Encoding.UTF8.GetBytes(msg);
				await netStream.WriteAsync(msgBuffer, 0, msgBuffer.Length);
			}
			else if (msg.StartsWith("name:"))
			{
				// Okay, so they might be a messenger
				string name = msg[(msg.IndexOf(':') + 1)..];

				if (ShowDetailedOutput)
				{
					Console.WriteLine("=======Signing in with name===========");
					Console.WriteLine($"Name is {name}");
				}

				if ((name != string.Empty) && (!_names.ContainsValue(name)))
				{
					// They're new here, add them in
					good = true;
					_names.Add(newClient, name);
					_messengers.Add(newClient);
					_viewers.Add(newClient);

					if (ShowDetailedOutput)
					{
						Console.WriteLine($"====================Adding CHAT client as viewer @ {addr}======================");
					}
					ShowMessengers();
					ShowViewers();

					if (ShowDetailedOutput)
					{
						Console.WriteLine("{0} is a Messenger with the name {1}.", endPoint, name);
					}

					// Tell the viewers we have a new messenger
					_messageQueue.Enqueue(String.Format("{0} has joined the chat.", name));
				}
			}
			else
			{
				// Wasn't either a viewer or messenger, clean up anyways.
				if (ShowDetailedOutput)
				{
					Console.WriteLine("Wasn't able to identify {0} as a Viewer or Messenger.", endPoint);
				}
				CleanupClient(newClient);
			}
		}

		// Do we really want them?
		if (!good)
			newClient.Close();
	}

	private void ShowMessengers()
	{
		if (ShowDetailedOutput)
		{
			Console.WriteLine("ShowMessengers()");
		}

		if (_messengers.Count == 0)
		{
			if (ShowDetailedOutput)
			{
				Console.WriteLine("Messengers has 0 items");
			}
				return;
		}

		int i = 1;
		Console.WriteLine($"Messengers: ({_messengers.Count})");
		foreach (TcpClient v in _messengers.ToArray())
		{
			if (v.Client.RemoteEndPoint is null)
				continue;
			var ip = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Address.ToString());
			var port = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Port.ToString());
			var addr = $"{ip}:{port}";

			// Get username
			// Dictionary<TcpClient, string> _names = [];
			foreach(KeyValuePair<TcpClient, string> entry in _names)
			{
				if (entry.Key == v)
				{
					Console.WriteLine($"{i++}. {addr} ({entry.Value})");
					break;
				}
			}
		}
		if (ShowDetailedOutput)
		{
			Console.WriteLine("End ShowMessengers()");
		}
	}

	private void ShowViewers()
	{
		if (ShowDetailedOutput)
		{
			Console.WriteLine("ShowViewers()");
		}

		if (_viewers.Count == 0)
		{
			if (ShowDetailedOutput)
			{
				Console.WriteLine("Viewers has 0 items");
			}
			return;
		}

		int i = 1;
		Console.WriteLine($"Viewers: ({_viewers.Count})");
		foreach (TcpClient v in _viewers.ToArray())
		{
			if (v.Client.RemoteEndPoint is null)
				continue;
			var ip = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Address.ToString());
			var port = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Port.ToString());
			var addr = $"{ip}:{port}";

			// Get username
			// Dictionary<TcpClient, string> _names = [];
			foreach(KeyValuePair<TcpClient, string> entry in _names)
			{
				if (entry.Key == v)
				{
					Console.WriteLine($"{i++}. {addr} ({entry.Value})");
					break;
				}
			}
		}
		if (ShowDetailedOutput)
		{
			Console.WriteLine("End ShowViewers()");
		}
	}

	// Sees if any of the clients have left the chat server
	private void CheckForDisconnects()
	{
		// Check the viewers first
		foreach (TcpClient v in _viewers.ToArray())
		{
			if (IsDisconnected(v))
			{
				// Show their name?
				string chatUser = _names[v];

				Console.WriteLine("Viewer {0} has left ({1})",
					chatUser,
					v.Client.RemoteEndPoint);

				// cleanup on our end
				_messengers.Remove(v);     // Remove from list
				_viewers.Remove(v);     // Remove from list
				_names.Remove(v);
				CleanupClient(v);
			}
		}

		// Check the messengers second
		foreach (TcpClient m in _messengers.ToArray())
		{
			if (IsDisconnected(m))
			{
				// Get info about the messenger
				string name = _names[m];

				// Tell the viewers someone has left
				if (ShowDetailedOutput)
				{
				}
				Console.WriteLine("Messenger {0} has left.", name);
				_messageQueue.Enqueue(String.Format("{0} has left the chat", name));

				// clean up on our end
				_messengers.Remove(m);  // Remove from list
				_names.Remove(m);       // Remove taken name
				CleanupClient(m);
			}
		}
	}

	// See if any of our messengers have sent us a new message, put it in the queue
	private void CheckForNewMessages()
	{
		foreach (TcpClient m in _messengers)
		{
			int messageLength = m.Available;
			if (messageLength > 0)
			{
				// there is one!  get it
				byte[] msgBuffer = new byte[messageLength];
				m.GetStream().ReadAsync(msgBuffer, 0, msgBuffer.Length);     // Blocks

				// Attach a name to it and shove it into the queue
				string msg = String.Format("{0}: {1}",
					_names[m],
					Encoding.UTF8.GetString(msgBuffer));
				var msgSender = _names[m];
				Console.WriteLine($"Enqueuing message {msg} from {msgSender}");
				_messageQueue.Enqueue(msg);
			}
			else
			{

			}
		}
	}

	// Clears out the message queue (and sends it to all of the viewers
	private void SendMessages()
	{
		string? MessageToSend;
		string? Sender;
		string? Recipient;
		string? Action;

		foreach (string msg in _messageQueue)
		{
			Console.WriteLine($"Queue message: >{msg}<");
			MessageToSend = msg;
			Sender = null;
			Recipient = null;

			char[] charsToTrim = { ' ' };

			// Set Recipient
			// Set message

			if (msg.Contains(":"))
			{
				Console.WriteLine("msg has :");
				// Default command to "message"
				// Later: Look for "users"

				// It's a chat message, so format, decide if personal
				string[] MainMessage = msg.Split(":");

				// chat:user:Message....
				// chat:Message
				Sender = MainMessage[0].Trim(charsToTrim);
				Action = MainMessage[1].Trim(charsToTrim);
				Console.WriteLine($"Action is {Action}");

				if (Action == "listusers")
				{
					Recipient = Sender;

					if (ShowDetailedOutput)
						Console.WriteLine($"Request for names, Dict count {_names.Count}");

					// Dictionary _names: values = names
					string UserMsg;
					if (_names.Count == 0)
						UserMsg = "Users: (None)";
					else
					{
						string UserList = String.Join("\n", _names.Values); 
						UserMsg = $"Users:\n{UserList}";
					}	
					if (ShowDetailedOutput)
						Console.WriteLine($"Request for names: {UserMsg}");

					MessageToSend = UserMsg;

				}
				else if (Action == "help" || Action == "?")
				{
					MessageToSend = 
					"""
					Usage: ?/help: this screen
					chat:msg       Send msg to all users
					chat:user:msg  Send msg to user
					listusers      Show logged-on users
					quit           Exit chat program
					""";
					Sender = MainMessage[0].Trim(charsToTrim);

				}
				else if (Action == "chat")
				{
					if (MainMessage.Length == 4)
					{
						// Personal message
						// user:chat:recip:Message
						Console.WriteLine("Personal message");

						// Validate user logged on
						Recipient = MainMessage[2].Trim(charsToTrim);

						// Look for name in viewers
						if (!_names.Values.Contains(Recipient))
						{
							Console.WriteLine($"{Recipient} not logged on");
							MessageToSend = $"Sorry, no such user {Recipient}";
							Recipient = Sender;
						}
						else
						{
							Console.WriteLine($"{Recipient} logged on");

							MessageToSend = MainMessage[3].Trim(charsToTrim);
							MessageToSend = $"{Sender} sends you private message: {MessageToSend}";
							if (ShowDetailedOutput)
								Console.WriteLine($"Message {MessageToSend} from {Sender} to {Recipient}");
						}
					}
					else if (MainMessage.Length == 3)
					{
						// Broadcast message
						// sender:chat:msg
						Console.WriteLine("System-wide message");
						Sender = MainMessage[0].Trim(charsToTrim);
						Recipient = null;
						MessageToSend = MainMessage[2].Trim(charsToTrim);
						MessageToSend = $"{Sender} says to all: {MessageToSend}";

						if (ShowDetailedOutput)
							Console.WriteLine($"Message {MessageToSend} from {Sender} to (All)");
					}
				}
			}

			// Send the message to each viewer
			foreach (TcpClient v in _viewers)
			{
				if (v?.Client?.RemoteEndPoint is null)
					continue;

				var ip = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Address.ToString());
				var port = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Port.ToString());
				var addr = $"{ip}:{port}";
				if (ShowDetailedOutput)
					Console.WriteLine($"Sending message {MessageToSend} to viewer @ {addr}");

				if (Recipient is not null)
				{
					if (ShowDetailedOutput)
						Console.WriteLine($"Using specific recipient >{Recipient}<");

					//Look in Names
					foreach(KeyValuePair<TcpClient, string> entry in _names)
					{
						if (ShowDetailedOutput)
							Console.WriteLine($"Checking entry username name {entry.Value}, {Recipient}");
						if (entry.Value == Recipient && entry.Key == v)
						{
							if (ShowDetailedOutput)
								Console.WriteLine($"Found match, send to them");
							byte[] msgBuffer = Encoding.UTF8.GetBytes(MessageToSend);
							v.GetStream().WriteAsync(msgBuffer, 0, msgBuffer.Length);    // Blocks
							break;
						}
					}

				}
				else
				{
					if (ShowDetailedOutput)
						Console.WriteLine($"Recipient null, sending to all recipients");
					byte[] msgBuffer = Encoding.UTF8.GetBytes(MessageToSend);
					v.GetStream().WriteAsync(msgBuffer, 0, msgBuffer.Length);    // Blocks
				}
			}
		}

		// clear out the queue
		_messageQueue.Clear();
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
		catch(SocketException)
		{
			return true;
		}
		catch(NullReferenceException)
		{
			return true;
		}
	}

	// cleans up resources for a TcpClient
	private static void CleanupClient(TcpClient client)
	{
		try
		{
			client.GetStream().Close();     // Close network stream
			client.Close();                 // Close client
		}
		catch (Exception) {}
	}

	protected void InterruptHandler(object? sender, ConsoleCancelEventArgs args)
	{
		Shutdown();
		args.Cancel = true;
	}

	public void Start()
	{
		Console.WriteLine("Server:Start()");

		ChatName = _config.ServerDisplayName ?? "(Default Name)";
		ServerAddress  = _config.ServerAddress;
		Port = _config.ServerPort;
		ShowDetailedOutput = _config.ShowDetailedOutput;

		if (ShowDetailedOutput)
		{
			Console.WriteLine($"Read config settings {ChatName}, {ServerAddress}, {Port}, Details: {ShowDetailedOutput}");
		}
		_listener = new TcpListener(IPAddress.Parse(ServerAddress ?? String.Empty), Port);
		Run();
	}
}

