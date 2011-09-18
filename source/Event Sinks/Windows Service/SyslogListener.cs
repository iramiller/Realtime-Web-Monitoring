using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebMonitoringSink.Messages;

namespace WebMonitoringSink
{
	public class SyslogListener
	{
		/// <summary>
		/// Address of the interface to bind to, mostly this should be IPAddress.Any
		/// </summary>
		private IPAddress _listenAddress;
		/// <summary>
		/// Port we are binding to listen on
		/// </summary>
		private int _listenPort;
		/// <summary>
		/// 
		/// </summary>
		private Socket _listenSocket;

		/// <summary>
		/// Packet Size (MTU)
		/// </summary>
		private int _packetSize;

		private const int DEFAULTPORT = 514;
		private const int DEFAULTPACKETSIZE = 1024;

		/// <summary>
		/// Buffer to hold data being read from the stream
		/// </summary>
		private byte[] _receiveBuffer;

		/// <summary>
		/// Event to signal a coordinated shutdown
		/// </summary>
		private CancellationTokenSource _cancelListening;
		/// <summary>
		/// Queue to hold the packet data
		/// </summary>
		private BlockingCollection<Tuple<string, IList<Byte>>> _queue;



		/// <summary>
		/// Creates a new instance of the SyslogListener class.
		/// </summary>
		/// <param name="listenAddress">Specifies the address to listen on.  IPAddress.Any will bind the listener to all available interfaces.</param>
		public SyslogListener(IPAddress listenAddress) : this(listenAddress, DEFAULTPORT, DEFAULTPACKETSIZE) { }
		/// <summary>
		///  Creates a new instance of the SyslogListener class.
		/// </summary>
		/// <param name="listenAddress">Specifies the address to listen on.  IPAddress.Any will bind the listener to all available interfaces.</param>
		/// <param name="port">the port to listen on</param>
		public SyslogListener(IPAddress listenAddress, int port) : this(listenAddress, port, DEFAULTPACKETSIZE) { }
		/// <summary>
		///  Creates a new instance of the SyslogListener class.
		/// </summary>
		/// <param name="listenAddress">Specifies the address to listen on.  IPAddress.Any will bind the listener to all available interfaces.</param>
		/// <param name="port">the port to listen on</param>
		/// <param name="packetSize">the size of the packets being received (this should be your minimum network MTU - default is 1024)</param>
		public SyslogListener(IPAddress listenAddress, int port, int packetSize)
		{
			_listenPort = port;
			_packetSize = packetSize;
			_receiveBuffer = new Byte[_packetSize];
			_listenAddress = listenAddress;
			_listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			_queue = new BlockingCollection<Tuple<string, IList<Byte>>>();
			_cancelListening = new CancellationTokenSource();
		}

		/// <summary>
		/// Starts listening for syslog packets.
		/// </summary>
		public void Start()
		{
			if (_listenSocket.IsBound) return;

			_listenSocket.Bind(new IPEndPoint(_listenAddress, _listenPort));
			if (_cancelListening.IsCancellationRequested)
				_cancelListening = new CancellationTokenSource();

			_cancelListening.Token.Register(() => { if (_listenSocket.IsBound) { _listenSocket.Close(); } _queue.CompleteAdding(); });

			StartListening();
		}

		public void Stop()
		{
			_cancelListening.Cancel();
		}

		/// <summary>
		/// Returns true if a message could be picked up before the timeout.
		/// </summary>
		/// <param name="timeout">time to wait in milliseconds. Use System.Theading.Timeout.Infinite (-1) to wait indefinately</param>
		/// <param name="message">the message received</param>
		/// <returns>true if a message could be picked up otherwise false.</returns>
		public bool TryPickupMessage(int timeout, out IMessage message)
		{
			Tuple<string, IList<Byte>> data;
			try
			{
				if (_queue.TryTake(out data, timeout, _cancelListening.Token))
				{
					message = new SyslogMessage(data.Item1, data.Item2);
					return true;
				}
			}
			catch (OperationCanceledException) { }
			message = null;
			return false;
		}

		/// <summary>
		/// Sets the socket up to listen for incoming data
		/// </summary>
		private void StartListening()
		{
			EndPoint remoteEndpoint = new IPEndPoint(IPAddress.None, 0);
			_listenSocket.BeginReceiveFrom(_receiveBuffer, 0, _packetSize, SocketFlags.None, ref remoteEndpoint, new AsyncCallback(OnReceiveData), _listenSocket);
		}

		/// <summary>
		/// This is called when data is incoming.  Data is taken from the socket and added to our queue for faster processing
		/// </summary>
		private void OnReceiveData(IAsyncResult ar)
		{
			Socket socket = ar.AsyncState as Socket;
			EndPoint remoteEndpoint = new IPEndPoint(IPAddress.None, 0);
			try
			{
				int bytesRead = socket.EndReceiveFrom(ar, ref remoteEndpoint);
				var r = (remoteEndpoint as IPEndPoint).Address.ToString();
				_queue.Add(new Tuple<string, IList<byte>>(r, _receiveBuffer.Where((b, i) => i < bytesRead).ToList()));
			}
			catch (SocketException) { } // buffer over runs, client disconnections (should not be possible but alas..)
			catch (ObjectDisposedException) { _cancelListening.Cancel(); } // object has been disposed so we quit.
			catch (InvalidOperationException) { } // thrown if adding to the queue fails... well, with UDP some messages are lost anyways...

			if (!_cancelListening.IsCancellationRequested)
				socket.BeginReceiveFrom(_receiveBuffer, 0, _packetSize, SocketFlags.None, ref remoteEndpoint, new AsyncCallback(OnReceiveData), _listenSocket);
		}
	}
}
