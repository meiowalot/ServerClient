// Filename:  TcpChatMessenger.cs        
// Author:    Benjamin N. Summerton <define-private-public>        
// License:   Unlicense (http://unlicense.org/)        

using Microsoft.AspNetCore.Mvc;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpChatMessenger;

class TcpChatMessenger
{
		// Viewer
		private bool _disconnectRequested = false;

		// Connection objects
		public readonly string ServerAddress;
		public readonly int Port;
		private TcpClient _client;
		public bool Running { get; private set; }

		// Buffer & messaging
		public readonly int BufferSize = 2 * 1024;  // 2KB
		private NetworkStream _msgStream = null;

		// Personal data
		public readonly string Name;

		public TcpChatMessenger(string serverAddress, int port, string name)
		{
				// Create a non-connected TcpClient
				_client = new TcpClient();          // Other constructors will start a connection
				_client.SendBufferSize = BufferSize;
				_client.ReceiveBufferSize = BufferSize;
				Running = false;

				// Set the other things
				ServerAddress = serverAddress;
				Port = port;
				Name = name;
		}

		public void Connect()
		{
				// Try to connect
				_client.Connect(ServerAddress, Port);       // Will resolve DNS for us; blocks
				EndPoint endPoint = _client.Client.RemoteEndPoint;

				// Make sure we're connected
				if (_client.Connected)
				{
						// Got in!
						Console.WriteLine("Connected to the server at {0}.", endPoint);

						// Tell them that we're a messenger
						_msgStream = _client.GetStream();
						byte[] msgBuffer = Encoding.UTF8.GetBytes(String.Format("name:{0}", Name));
						_msgStream.Write(msgBuffer, 0, msgBuffer.Length);   // Blocks

						// If we're still connected after sending our name, that means the server accepts us
						if (!_isDisconnected(_client))
								Running = true;
						else
						{
								// Name was probably taken...
								_cleanupNetworkResources();
								Console.WriteLine("The server rejected us; \"{0}\" is probably in use.", Name);
						}
				}
				else
				{
						_cleanupNetworkResources();
						Console.WriteLine("Wasn't able to connect to the server at {0}.", endPoint);
				}
		}

		public void HandleMessages() 
		{
				bool wasRunning = Running;
				Console.WriteLine("HandleMessages()");

//				Task.Run(()=>{
				//Here is a new thread
				SendMessages(); 
//				});

				Task.Run(()=>{
				//Here is a new thread
				ListenForMessages(); 
				});

/*
				while (Running)
				{
						// Send or receive?

						// Poll for user input
						Console.Write("{0}> ", Name);
						string msg = Console.ReadLine();

						Console.Write("Probably won't get here, waiting for input");

						// Quit or send a message
						if ((msg.ToLower() == "quit") || (msg.ToLower() == "exit"))
						{
								// User wants to quit
								Console.WriteLine("Disconnecting...");
								Running = false;
						}
						else if (msg != string.Empty)
						{
								// Send the message
								Console.WriteLine($"Sending message {msg}");
								byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);
								_msgStream.Write(msgBuffer, 0, msgBuffer.Length);   // Blocks
						}

						// Use less CPU
						Thread.Sleep(10);

						// Check the server didn't disconnect us
						if (_isDisconnected(_client))
						{
								Running = false;
								Console.WriteLine("Server has disconnected from us.\n:[");
						}
				}

				_cleanupNetworkResources();
				if (wasRunning)
						Console.WriteLine("Disconnected.");
*/
		}

		public void SendMessages()
		{
				bool wasRunning = Running;

				Console.WriteLine("SendMessages(), while loop:");
				Console.WriteLine("Entering SendMessages while loop");
				while (Running)
				{
						Console.WriteLine("Inside SendMessages while loop");

						// Poll for user input
						Console.Write("{0}> ", Name);
						string msg = Console.ReadLine();

						// Quit or send a message
						if ((msg.ToLower() == "quit") || (msg.ToLower() == "exit"))
						{
								// User wants to quit
								Console.WriteLine("Disconnecting...");
								Running = false;
						}
						else if (msg != string.Empty)
						{
								// Send the message
								Console.WriteLine($"Sending message {msg}");
								byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);
								_msgStream.Write(msgBuffer, 0, msgBuffer.Length);   // Blocks
						}

						// Use less CPU
						Thread.Sleep(10);

						// Check the server didn't disconnect us
						if (_isDisconnected(_client))
						{
								Running = false;
								Console.WriteLine("Server has disconnected from us.\n:[");
						}
				}

				_cleanupNetworkResources();
				if (wasRunning)
						Console.WriteLine("Disconnected.");

				Console.WriteLine($"Leaving SendMessages()");
		}

		// Cleans any leftover network resources
		private void _cleanupNetworkResources()
		{
				_msgStream?.Close();
				_msgStream = null;
				_client.Close();
		}

		// Checks if a socket has disconnected
		// Adapted from -- http://stackoverflow.com/questions/722240/instantly-detect-client-disconnection-from-server-socket
		private static bool _isDisconnected(TcpClient client)
		{
				try
				{
						Socket s = client.Client;
						return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
				}
				catch(SocketException se)
				{
						// We got a socket error, assume it's disconnected
						return true;
				}
		}


		// connects to the chat server
		public void ConnectViewer()
		{
				// Now try to connect
				//_client.Connect(ServerAddress, Port);   // Will resolve DNS for us; blocks
		    //	EndPoint endPoint = _client.Client.RemoteEndPoint;

				// Send them the message that we're a viewer
				_msgStream = _client.GetStream();
				byte[] msgBufferViewer = Encoding.UTF8.GetBytes("viewer");
				_msgStream.Write(msgBufferViewer, 0, msgBufferViewer.Length);     // Blocks

				return;


/*
				// check that we're connected
				if (_client.Connected)
				{
						// got in!
						Console.WriteLine("Connected to the server at {0}.", endPoint);

						// Send them the message that we're a viewer
						_msgStream = _client.GetStream();
						byte[] msgBuffer = Encoding.UTF8.GetBytes("viewer");
						_msgStream.Write(msgBuffer, 0, msgBuffer.Length);     // Blocks

						// check that we're still connected, if the server has not kicked us, then we're in!
						if (!_isDisconnected(_client))
						{
								Running = true;
								Console.WriteLine("Press Ctrl-C to exit the Viewer at any time.");
						}
						else
						{
								// Server doens't see us as a viewer, cleanup
								_cleanupNetworkResources();
								Console.WriteLine("The server didn't recognise us as a Viewer.\n:[");
						}
				}
				else
				{
						_cleanupNetworkResources();
						Console.WriteLine("Wasn't able to connect to the server at {0}.", endPoint);
				}
*/	
		}

		// Main loop, listens and prints messages from the server
		public void ListenForMessages()
		{
				bool wasRunning = Running;

				Console.WriteLine("ListenForMessages(), while loop:");
				if (_client is null)
					Console.WriteLine("LFM: client is null");
				// Listen for messages
				while (Running)
				{
						// Do we have a new message?
						int messageLength = _client.Available;
						Console.WriteLine($"Got message, length {messageLength}");
						if (messageLength > 0)
						{
								Console.WriteLine("New incoming message of {0} bytes", messageLength);

								// Read the whole message
								byte[] msgBuffer = new byte[messageLength];
								_msgStream.Read(msgBuffer, 0, messageLength);   // Blocks

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
								Console.WriteLine(msg);
						}

						// Use less CPU
						Thread.Sleep(10);

						// Check the server didn't disconnect us
						if (_isDisconnected(_client))
						{
								Running = false;
								Console.WriteLine("Server has disconnected from us.\n:[");
						}

						// Check that a cancel has been requested by the user
						Running &= !_disconnectRequested;
				}

				// Cleanup
				_cleanupNetworkResources();
				if (wasRunning)
						Console.WriteLine("Disconnected.");

				//return "Hello";
				Console.WriteLine($"Leaving ListenForMessages()");
		}

		public static void Main(string[] args)
		{
				// Get a name
				Console.Write("Enter a name to use: ");
				string name = Console.ReadLine();

				// Setup the Messenger
				//string host = "localhost";//args[0].Trim();
				string host = "10.0.1.201";//args[0].Trim();
				int port = 6000;//int.Parse(args[1].Trim());
				TcpChatMessenger messenger = new TcpChatMessenger(host, port, name);

				// connect and send messages
				messenger.Connect();
				messenger.ConnectViewer();
				//messenger.SendMessages();
				//messenger.ListenForMessages();
				messenger.HandleMessages();
		}

/*

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpChatViewer
{
    class TcpChatViewer
    {
        // Connection objects
        public readonly string ServerAddress;
        public readonly int Port;
        private TcpClient _client;
        public bool Running { get; private set; }
        private bool _disconnectRequested = false;

        // Buffer & messaging
        public readonly int BufferSize = 2 * 1024;  // 2KB
        private NetworkStream _msgStream = null;

        public TcpChatViewer(string serverAddress, int port)
        {
            // Create a non-connected TcpClient
            _client = new TcpClient();          // Other constructors will start a connection
            _client.SendBufferSize = BufferSize;
            _client.ReceiveBufferSize = BufferSize;
            Running = false;

            // Set the other things
            ServerAddress = serverAddress;
            Port = port;
        }

        // connects to the chat server
        public void Connect()
        {
            // Now try to connect
            _client.Connect(ServerAddress, Port);   // Will resolve DNS for us; blocks
            EndPoint endPoint = _client.Client.RemoteEndPoint;

            // check that we're connected
            if (_client.Connected)
            {
                // got in!
                Console.WriteLine("Connected to the server at {0}.", endPoint);

                // Send them the message that we're a viewer
                _msgStream = _client.GetStream();
                byte[] msgBuffer = Encoding.UTF8.GetBytes("viewer");
                _msgStream.Write(msgBuffer, 0, msgBuffer.Length);     // Blocks

                // check that we're still connected, if the server has not kicked us, then we're in!
                if (!_isDisconnected(_client))
                {
                    Running = true;
                    Console.WriteLine("Press Ctrl-C to exit the Viewer at any time.");
                }
                else
                {
                    // Server doens't see us as a viewer, cleanup
                    _cleanupNetworkResources();
                    Console.WriteLine("The server didn't recognise us as a Viewer.\n:[");
                }
            }
            else
            {
                _cleanupNetworkResources();
                Console.WriteLine("Wasn't able to connect to the server at {0}.", endPoint);
            }
        }

        // Requests a disconnect
        public void Disconnect()
        {
            Running = false;
            _disconnectRequested = true;
            Console.WriteLine("Disconnecting from the chat...");
        }

        // Main loop, listens and prints messages from the server
        public void ListenForMessages()
        {
            bool wasRunning = Running;

            // Listen for messages
            while (Running)
            {
                // Do we have a new message?
                int messageLength = _client.Available;
                if (messageLength > 0)
                {
                    //Console.WriteLine("New incoming message of {0} bytes", messageLength);

                    // Read the whole message
                    byte[] msgBuffer = new byte[messageLength];
                    _msgStream.Read(msgBuffer, 0, messageLength);   // Blocks

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
                    Console.WriteLine(msg);
                }

                // Use less CPU
                Thread.Sleep(10);

                // Check the server didn't disconnect us
                if (_isDisconnected(_client))
                {
                    Running = false;
                    Console.WriteLine("Server has disconnected from us.\n:[");
                }

                // Check that a cancel has been requested by the user
                Running &= !_disconnectRequested;
            }

            // Cleanup
            _cleanupNetworkResources();
            if (wasRunning)
                Console.WriteLine("Disconnected.");
        }

        // Cleans any leftover network resources
        private void _cleanupNetworkResources()
        {
            _msgStream?.Close();
            _msgStream = null;
            _client.Close();
        }

        // Checks if a socket has disconnected
        // Adapted from -- http://stackoverflow.com/questions/722240/instantly-detect-client-disconnection-from-server-socket
        private static bool _isDisconnected(TcpClient client)
        {
            try
            {
                Socket s = client.Client;
                return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
            }
            catch(SocketException se)
            {
                // We got a socket error, assume it's disconnected
                return true;
            }
        }

        public static TcpChatViewer viewer;

        protected static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
        {
            viewer.Disconnect();
            args.Cancel = true;
        }

        public static void NotMain(string[] args)
        {
            // Setup the Viewer
            //string host = "localhost";//args[0].Trim();
            string host = "10.0.1.201";//args[0].Trim();
            int port = 6000;//int.Parse(args[1].Trim());
            viewer = new TcpChatViewer(host, port);

            // Add a handler for a Ctrl-C press
            Console.CancelKeyPress += InterruptHandler;

            // Try to connect & view messages
            viewer.Connect();
            viewer.ListenForMessages();
        }
    }
}

*/

} // class

