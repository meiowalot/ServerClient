// Filename:  TcpChatServer.cs        
// Author:    Benjamin N. Summerton <define-private-public>        
// License:   Unlicense (http://unlicense.org/)        

using System;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpChatServer
{
    class TcpChatServer
    {
        // What listens in
        private TcpListener _listener;

        // types of clients connected
        private List<TcpClient> _viewers = new List<TcpClient>();
        private List<TcpClient> _messengers = new List<TcpClient>();

        // Names that are taken by other messengers
        private Dictionary<TcpClient, string> _names = new Dictionary<TcpClient, string>();

        // Messages that need to be sent
        private Queue<string> _messageQueue = new Queue<string>();

        // Extra fun data
        public readonly string ChatName;
        public readonly int Port;
        public bool Running { get; private set; }

        // Buffer
        public readonly int BufferSize = 2 * 1024;  // 2KB

        // Make a new TCP chat server, with our provided name
        public TcpChatServer(string chatName, int port)
        {
            // Set the basic data
            ChatName = chatName;
            Port = port;
            Running = false;

            // Make the listener listen for connections on any network device
            _listener = new TcpListener(IPAddress.Any, Port);
        }

        // If the server is running, this will shut down the server
        public void Shutdown()
        {
            Running = false;
            Console.WriteLine("Shutting down server");
        }

        // Start running the server.  Will stop when `Shutdown()` has been called
        public void Run()
        {
            // Some info
            Console.WriteLine("Starting the \"{0}\" TCP Chat Server on port {1}.", ChatName, Port);
            Console.WriteLine("Press Ctrl-C to shut down the server at any time.");

            // Make the server run
            _listener.Start();           // No backlog
            Running = true;

            // Main server loop
            while (Running)
            {
                // Check for new clients
                if (_listener.Pending())
                    _handleNewConnection();

                // Do the rest
                _checkForDisconnects();
                _checkForNewMessages();
                _sendMessages();

                // Use less CPU
                Thread.Sleep(10);
            }

            // Stop the server, and clean up any connected clients
            foreach (TcpClient v in _viewers)
                _cleanupClient(v);
            foreach (TcpClient m in _messengers)
                _cleanupClient(m);
            _listener.Stop();

            // Some info
            Console.WriteLine("Server is shut down.");
        }
            
        private async void _handleNewConnection()
        {
            // There is (at least) one, see what they want
            bool good = false;
            TcpClient newClient = _listener.AcceptTcpClient();      // Blocks
            NetworkStream netStream = newClient.GetStream();

            // Modify the default buffer sizes
            newClient.SendBufferSize = BufferSize;
            newClient.ReceiveBufferSize = BufferSize;

            // Print some info
            EndPoint endPoint = newClient.Client.RemoteEndPoint;
            Console.WriteLine("Handling a new client from {0}...", endPoint);

            // Let them identify themselves
            byte[] msgBuffer = new byte[BufferSize];
            int bytesRead = await netStream.ReadAsync(msgBuffer, 0, msgBuffer.Length);     // Blocks
            //Console.WriteLine("Got {0} bytes.", bytesRead);
            if (bytesRead > 0)
            {
                string msg = Encoding.UTF8.GetString(msgBuffer, 0, bytesRead);
								var ip = IPAddress.Parse(((IPEndPoint)endPoint).Address.ToString());
								var port = IPAddress.Parse(((IPEndPoint)endPoint).Port.ToString());
								var addr = $"{ip}:{port}";

                if (msg == "viewer")
                {
                    // They just want to watch
										Console.WriteLine($"====================Adding viewer @ {addr}======================");
                    good = true;
                    _viewers.Add(newClient);
										ShowViewers();

                    Console.WriteLine("{0} is a Viewer.", endPoint);

                    // Send them a "hello message"
                    msg = String.Format("Welcome to the \"{0}\" Chat Server!", ChatName);
                    msgBuffer = Encoding.UTF8.GetBytes(msg);
                    netStream.WriteAsync(msgBuffer, 0, msgBuffer.Length);    // Blocks
                }
                else if (msg.StartsWith("name:"))
                {
                    // Okay, so they might be a messenger
                    string name = msg.Substring(msg.IndexOf(':') + 1);

                    if ((name != string.Empty) && (!_names.ContainsValue(name)))
                    {
                        // They're new here, add them in
                        good = true;
                        _names.Add(newClient, name);
                        _messengers.Add(newClient);

												Console.WriteLine($"====================Adding CHAT client as viewer @ {addr}======================");
												_viewers.Add(newClient);
												ShowViewers();

                        Console.WriteLine("{0} is a Messenger with the name {1}.", endPoint, name);

                        // Tell the viewers we have a new messenger
                        _messageQueue.Enqueue(String.Format("{0} has joined the chat.", name));
                    }
                }
                else
                {
                    // Wasn't either a viewer or messenger, clean up anyways.
                    Console.WriteLine("Wasn't able to identify {0} as a Viewer or Messenger.", endPoint);
                    _cleanupClient(newClient);
                }
            }

            // Do we really want them?
            if (!good)
                newClient.Close();
        }

				private void ShowViewers()
				{
						if (_viewers.Count == 0)
						{
							Console.WriteLine("Viewers has 0 items");
							return;
						}
	
						int i = 0;	
						Console.WriteLine("Viewers:");
            foreach (TcpClient v in _viewers.ToArray())
						{
								var ip = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Address.ToString());
								var port = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Port.ToString());
								var addr = $"{ip}:{port}";
								Console.WriteLine($"{++i}. {addr}");

						}

				}

        // Sees if any of the clients have left the chat server
        private void _checkForDisconnects()
        {
            // Check the viewers first
            foreach (TcpClient v in _viewers.ToArray())
            {
                if (_isDisconnected(v))
                {
                    Console.WriteLine("Viewer {0} has left.", v.Client.RemoteEndPoint);

                    // cleanup on our end
                    _viewers.Remove(v);     // Remove from list
                    _cleanupClient(v);
                }
            }

            // Check the messengers second
            foreach (TcpClient m in _messengers.ToArray())
            {
                if (_isDisconnected(m))
                {
                    // Get info about the messenger
                    string name = _names[m];

                    // Tell the viewers someone has left
                    Console.WriteLine("Messeger {0} has left.", name);
                    _messageQueue.Enqueue(String.Format("{0} has left the chat", name));

                    // clean up on our end 
                    _messengers.Remove(m);  // Remove from list
                    _names.Remove(m);       // Remove taken name
                    _cleanupClient(m);
                }
            }
        }

        // See if any of our messengers have sent us a new message, put it in the queue
        private void _checkForNewMessages()
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
                    string msg = String.Format("{0}: {1}", _names[m], Encoding.UTF8.GetString(msgBuffer));
										var msgSender = _names[m];	
										Console.WriteLine($"Enqueuing message {msg} from {msgSender}");
                    _messageQueue.Enqueue(msg);
                }
            }
        }

        // Clears out the message queue (and sends it to all of the viewers
        private void _sendMessages()
        {
            foreach (string msg in _messageQueue)
            {
                // Encode the message
                byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);

                // Send the message to each viewer
                foreach (TcpClient v in _viewers)
								{
										var ip = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Address.ToString());
										var port = IPAddress.Parse(((IPEndPoint)v.Client.RemoteEndPoint).Port.ToString());
										var addr = $"{ip}:{port}";
										Console.WriteLine($"Sending message {msg} to viewer @ {addr}");
                    v.GetStream().WriteAsync(msgBuffer, 0, msgBuffer.Length);    // Blocks
								}
            }

            // clear out the queue
            _messageQueue.Clear();
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

        // cleans up resources for a TcpClient
        private static void _cleanupClient(TcpClient client)
        {
            client.GetStream().Close();     // Close network stream
            client.Close();                 // Close client
        }

        public static TcpChatServer chat;

        protected static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
        {
            chat.Shutdown();
            args.Cancel = true;
        }
            
        public static void Main(string[] args)
        {
            // Create the server
            string name = "Bad IRC";//args[0].Trim();
            int port = 6000;//int.Parse(args[1].Trim());
            chat = new TcpChatServer(name, port);

            // Add a handler for a Ctrl-C press
            Console.CancelKeyPress += InterruptHandler;

            // run the chat server
            chat.Run();
        }
    }
}

