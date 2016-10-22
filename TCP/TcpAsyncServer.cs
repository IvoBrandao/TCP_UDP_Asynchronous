using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Collections;



namespace TCP
{

    // State object for reading client data asynchronously
    public class StateObject
    {
        // id of the client
        public int Id;
        // Size of receive buffer.
        public const int BufferSize = 1024;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Client  socket.
        public Socket ClientSocket = null;
    }

    public class TcpAsyncServer : IDisposable
    {
        public IPAddress Ip { get; set; }
        public int Port { get; set; }
        public SocketType SocketStype { get; set; }
        public ProtocolType ProtocolType { get; set; }

        // Private Variables
        // Queue to Store the Received Data 
        private static ConcurrentQueue<byte> RxData;
        // Manual Reset Event to Resume the Thread and Accepting a new client
        private static ManualResetEvent allDone = new ManualResetEvent(false);
        // Connected client list 
        private static Dictionary<int, StateObject> ClientList;
        // Indicates if the server is listening 
        private static bool IsListening;

        /// <summary>
        /// Constructor
        /// </summary>
        public TcpAsyncServer()
        {
            RxData = new ConcurrentQueue<byte>();
            ClientList = new Dictionary<int, StateObject>();
        }

        /// <summary>
        /// Start a Server in the port and Address given by the Ip and Port properties.
        /// </summary>
        public void Start()
        {
            if (IsListening == true) return;
            if (Ip == null) throw new System.ArgumentException("Parameter cannot be null", "IPAddress");
            else if (Port < 1024 || Port > 65535) throw new System.ArgumentException("Parameter Port value outside of range 1024-65535", "Port");
            else new Thread(() => StartListening()).Start();
        }

        /// <summary>
        /// Stops All the clients connected to the server and after that stops the server
        /// </summary>
        public void Stop()
        {
            
            try
            {
                // Stop listening 
                IsListening = false;
                // close all clients
                CloseAllClients();
                // Wait for the clients to disconnect before disconecting the server
                while (ClientList.Count > 0)
                {
                    Thread.Sleep(100);
                }
                // Signal the main thread to continue, and terminate the server
                allDone.Set();

            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                Console.WriteLine("[{0}] [Server Closed] ", DateTime.Now.ToString("hh:mm:ss"));
            }
        }

        /// <summary>
        /// Terminates all the clients connected to the server
        /// </summary>
        private void CloseAllClients()
        {
            try
            {
                // For each connected client
                foreach (KeyValuePair<int, StateObject> pair in ClientList)
                {
                    if (pair.Value != null)
                    {
                        // Send the command to Shutdown the client 
                        pair.Value.ClientSocket.Shutdown(SocketShutdown.Send);
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Checks if the socket is connected
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool IsConnected(int id)
        {
            // Do a request poll client 
            //  return !(state.ClientSocket.Poll(1000, SelectMode.SelectRead) && state.ClientSocket.Available == 0);
            return true;
        }

        /// <summary>
        ///  Start the TCP Aynchrounous server in tbe given IP an Port
        /// </summary>
        public void StartListening()
        {
            // LOG
            Console.WriteLine("[{0}] [Server Starting]", DateTime.Now.ToString("hh:mm:ss"));

            try
            {
                // Establish the local endpoint for the socket.
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, Port);
                // Create a TCP/IP socket.
                Socket ServerSocket = new Socket(AddressFamily.InterNetwork, SocketStype, ProtocolType);
                // Bind the socket to the local endpoint and listen for incoming connections.
                ServerSocket.Bind(localEndPoint);
                // listen for 100
                ServerSocket.Listen(100);
                // Socket in non blocking mode
                ServerSocket.Blocking = false;
                // Start listening
                IsListening = true;
                // LOG
                Console.WriteLine("[{0}] [Server Running] @ {1}", DateTime.Now.ToString("hh:mm:ss"), localEndPoint.ToString());

                while (IsListening == true)
                {
                    // Set the event to nonsignaled state.
                    allDone.Reset();
                    // Start an asynchronous socket to listen for connections.
                    ServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), ServerSocket);
                    // Wait until a connection is made before continuing.
                    allDone.WaitOne();
                }

                // Stop Server :)
                ServerSocket.Close();
            }
            catch (Exception e)
            {
                throw e;
            }

        }

        /// <summary>
        /// Accepts a new client connection assynchrounously
        /// </summary>
        /// <param name="ar"></param>
        public static void AcceptCallback(IAsyncResult ar)
        {

            // If we are closing the server stop accepting clients
            if (IsListening == false)
            {
                return;
            }

            // Signal the main thread to continue.
            allDone.Set();
            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            // End Accept process
            Socket handler = listener.EndAccept(ar);
            // The new socket is not blocking
            handler.Blocking = false;
            // No linger state
            handler.LingerState.Enabled = true;
            // Timeout 
            handler.LingerState.LingerTime = 0;
            // Create the state object.
            StateObject state = new StateObject();
            // Pass the socket handler to the stateobject
            state.ClientSocket = handler;
            // Log
            Console.WriteLine("[{0}] [New Client] @ {1}", DateTime.Now.ToString("hh:mm:ss"), handler.RemoteEndPoint.ToString());
            // Begin Receiving Data
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);
            // Maintain connected clients
            state.Id = ClientList.Count + 1;
            ClientList.Add(state.Id, state);
        }

        /// <summary>
        /// Reads data sent by the client
        /// </summary>
        /// <param name="ar"></param>
        public static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {


        ASCIIEncoding enc = new ASCIIEncoding();
        // Retrieve the state object and the handler socket from the asynchronous state object.
        StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.ClientSocket;
                // Read data from the client socket. 
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0) // Has data to be read
                {
                    for (int i = 0; i < bytesRead; i++)
                    {
                        // Enqueue Rx data
                        RxData.Enqueue(state.buffer[i]);
                    }
                    if (IsListening == true)
                    {
                        // There  might be more data, so store the data received so far.
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                    }
                    // Log
                    Console.WriteLine("[{0}]" + " [Server Rx] Length:{1} \n" + BitConverter.ToString(state.buffer, 0, bytesRead), DateTime.Now.ToString("hh:mm:ss"), bytesRead);
                    //Console.WriteLine("[{0}]" + " [Server Rx] Length:{1} \n" + enc.GetString(state.buffer, 0, bytesRead), DateTime.Now.ToString("hh:mm:ss"), bytesRead);

                }
                else if (bytesRead == 0)
                {
                    // The Client Side is disconnecting
                    // ShutDown the sokect 
                    handler.Shutdown(SocketShutdown.Both);
                    // Begin the disconnecting
                    handler.BeginDisconnect(true, new AsyncCallback(DisconnectCallback), state);
                }
            }
            catch (Exception e)
            {

                throw e;
            }
        }

        /// <summary>
        /// Callback to disconnect from a client socket
        /// </summary>
        /// <param name="ar"></param>
        private static void DisconnectCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.ClientSocket;
                // Remove the client from the client list
                ClientList.Remove(state.Id);
                // Store the Remote endpoint to inform that the user, that the endpoint has disconnected
                EndPoint RemotEndPoint = handler.RemoteEndPoint;
                // Complete the disconnect request
                handler.EndDisconnect(ar);
                // Close the socket
                handler.Close();
                // Dispose the socket
                handler.Dispose();
                // LOG
                Console.WriteLine("[{0}] [Client Disconnected] @ {1}", DateTime.Now.ToString("hh:mm:ss"), RemotEndPoint.ToString());
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Send data to a Connected EndPoint
        /// </summary>
        /// <param name="RemoteEndPointID"></param>
        /// <param name="data"></param>
        public bool Send(int RemoteEndPointID, byte[] data)
        {
            if (data == null) return false;
            bool clientfound = false;
            try
            {
                // Look for the client ID
                foreach (KeyValuePair<int, StateObject> pair in ClientList)
                {
                    // if the client ID is still connected
                    if (pair.Value.Id == RemoteEndPointID)
                    {
                        // Send Data to the endpoint
                        SendDataTo(pair.Value.ClientSocket, data);
                        clientfound = true;
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (clientfound == true)
                    Console.WriteLine("[{0}] [Remote EndPoint Is Connected]", DateTime.Now.ToString("hh:mm:ss"));
                else
                    Console.WriteLine("[{0}] [Remote EndPoint Not Connected]", DateTime.Now.ToString("hh:mm:ss"));
            }
            return clientfound;
        }

        /// <summary>
        /// Send data to the Remote Endpoint 
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="data"></param>
        private static void SendDataTo(Socket handler, byte[] data)
        {
            try
            {
                if (handler == null) return;
                // LOG
                Console.WriteLine("[{0}]" + " [Server Tx] " + BitConverter.ToString(data, 0, data.Length), DateTime.Now.ToString("hh:mm:ss"));
                // Begin sending the data to the remote device.
                handler.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), handler);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Send data CallBack
        /// </summary>
        /// <param name="ar"></param>
        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket handler = (Socket)ar.AsyncState;
                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
                // LOG
                Console.WriteLine("[{0}]" + " [Data Sent] Length:{1}  ", DateTime.Now.ToString("hh:mm:ss"), bytesSent);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop();
                }
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~TcpAsyncClient() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
