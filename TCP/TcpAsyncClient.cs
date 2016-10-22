using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;

namespace TCP
{
   
    public class TcpAsyncClient :IDisposable
    {
        public IPAddress Ip { get; set; }
        public int Port { get; set; }
        public SocketType SocketStype { get; set; }
        public ProtocolType ProtocolType { get; set; }

        private static bool IsConnected;
        private static ConcurrentQueue<byte> RxData;
        private static Socket Currentclient;
        public TcpAsyncClient()
        {
            IsConnected = false;
            RxData = new ConcurrentQueue<byte>();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Start()
        {

            if (IsConnected == true) return;
            if (Ip == null) throw new System.ArgumentException("Parameter cannot be null", "IPAddress");
            else if (Port < 1024 || Port > 65535) throw new System.ArgumentException("Parameter Port value outside of range 1024-65535", "Port");
            else new Thread(() => Connect()).Start();
        }
        private void Connect() { 


               if (IsConnected == true) return;
            if (Ip == null) throw new System.ArgumentException("Parameter cannot be null", "IPAddress");
            else if (Port < 1024 || Port > 65535) throw new System.ArgumentException("Parameter Port value outside of range 1024-65535", "Port");
            else new Thread(() => Connect()).Start();
            // Connect to a remote device.
            try
            {
                // Log
                Console.WriteLine("[{0}] [Client Starting]", DateTime.Now.ToString("hh:mm:ss"));
                // Establish the remote endpoint for the socket.
                IPEndPoint remoteEP = new IPEndPoint(Ip, Port);
                // Create a TCP/IP socket.
                Currentclient = new Socket(AddressFamily.InterNetwork,  SocketStype, ProtocolType);
                // Connect to the remote endpoint.
                Currentclient.BeginConnect(remoteEP,new AsyncCallback(ConnectCallback), Currentclient);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Stop()
        {
            if (IsConnected == true)
            {
                Currentclient.Shutdown(SocketShutdown.Send);
            }
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ar"></param>
        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = new StateObject();
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;
                // Store the Socket state
                state.ClientSocket = client;
                // Complete the connection.
                client.EndConnect(ar);
                // Signal that the connection has been made.
                IsConnected = true;
                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                // Log
                Console.WriteLine("[{0}] [Client Connected] @ {1}", DateTime.Now.ToString("hh:mm:ss"), client.LocalEndPoint.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ar"></param>
        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.ClientSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0) // Has data to be read
                {
                    for (int i = 0; i < bytesRead; i++)
                    {
                        // Enqueue Rx data
                        RxData.Enqueue(state.buffer[i]);
                    }
                    if (IsConnected == true)
                    {
                        // There  might be more data, so store the data received so far.
                        client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                    }
                    // Log
                    Console.WriteLine("[{0}]" + " [Client Rx] Length:{1} " + BitConverter.ToString(state.buffer, 0, bytesRead), DateTime.Now.ToString("hh:mm:ss"), bytesRead);
                }
                else if (bytesRead == 0)
                {
                    // The Server Side is disconnecting

                    // ShutDown the sokect 
                    client.Shutdown(SocketShutdown.Both);
                    // Begin the disconnecting
                    client.BeginDisconnect(true, new AsyncCallback(DisconnectCallback), state);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
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
                // Store the Remote endpoint to inform that the user, that the endpoint has disconnected
                EndPoint RemotEndPoint = handler.RemoteEndPoint;
                // Complete the disconnect request
                handler.EndDisconnect(ar);
                // Close the socket
                handler.Close();
                // Dispose the socket
                handler.Dispose();
                // indicate that the client has stoped
                IsConnected = false;
                // LOG
                Console.WriteLine("[{0}] [Client Stoped] @ {1}", DateTime.Now.ToString("hh:mm:ss"), RemotEndPoint.ToString());
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="data"></param>
        public bool Send(byte[] data)
        {
            if (data == null || Currentclient==null || IsConnected==false) return false;

            // Log
            Console.WriteLine("[{0}]" + " [Client Tx] " + BitConverter.ToString(data, 0, data.Length), DateTime.Now.ToString("hh:mm:ss"));
            // Begin sending the data to the remote device.
            Currentclient.BeginSend(data, 0, data.Length, 0,new AsyncCallback(SendCallback), Currentclient);

            return true;
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
