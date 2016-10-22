using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Net;

namespace TCP
{
    public partial class Form1 : Form
    {

        TcpAsyncServer server;
        TcpAsyncClient client;


        public Form1()
        {
            InitializeComponent();
            server = new TcpAsyncServer();
            client = new TcpAsyncClient();
        }

        // Start Server
        private void button1_Click(object sender, EventArgs e)
        {

            IPAddress ip;
            if (IPAddress.TryParse("192.168.0.166", out ip))
            {
                server.Ip = ip;
            }
            server.Port = 8888;
            server.SocketStype = System.Net.Sockets.SocketType.Stream;
            server.ProtocolType = System.Net.Sockets.ProtocolType.Tcp;
            server.Start();
        }
        // Stop Server
        private void button2_Click(object sender, EventArgs e)
        {
            server.Stop();
        }
        // Send From Server
        private void button3_Click(object sender, EventArgs e)
        {
            byte[] data = null;
            try
            {
                data = stringToBytes(textBox1.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK);
            }
            finally
            {
                if (!server.Send(1, data))
                {
                    MessageBox.Show("Check inputdata", "Error", MessageBoxButtons.OK);
                }
            }
        }
        // Closing the form
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // if the Window is closed and the server
            // is running terminate the connections before
            // closing the form 
            if (server != null)
                server.Dispose();
            if (client != null)
                client.Dispose();
               
        }


        public static byte[] stringToBytes(string text)
        {
            int length;
            Byte[] data;

            if (text.Length < 2)
                return null;

            text = text.Replace(" ", String.Empty);
            text = text.Replace("-", String.Empty);
            text = text.Replace("$", String.Empty);
            text = text.Replace("h", String.Empty);
            text = text.Replace("0x", String.Empty);
            text = text.Replace("{", String.Empty);
            text = text.Replace("}", String.Empty);
            text = text.Replace("<", String.Empty);
            text = text.Replace(">", String.Empty);


            length = text.Length / 2;
            data = new Byte[length];

            for (int i = 0; i < length; i++)
            {
                try
                {
                    data[i] = (Byte)Convert.ToInt32(text.Substring(i * 2, 2), 16);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return data;
        }

        public static string BytesToString(byte[] data)
        {
            string text = "";
            try
            {
                if (data != null)
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        //"Little-endian" means the most significant byte is on the right end of a word.

                        text = BitConverter.ToString(data);
                    }
                    else
                    {
                        // "Big-endian" means the most significant byte is on the left end of a word. 
                        Array.Reverse(data);
                        text = BitConverter.ToString(data);
                    }
                }
            }
            catch (Exception)
            {
                text = null;
            }

            return text;
        }

        // Start client
        private void button6_Click(object sender, EventArgs e)
        {
            IPAddress ip;
            if (IPAddress.TryParse("127.0.0.1", out ip))
            {
                client.Ip = ip;
            }
            client.Port = 64202;
            client.SocketStype = System.Net.Sockets.SocketType.Stream;
            client.ProtocolType = System.Net.Sockets.ProtocolType.Tcp;
            client.Start();
        }

        // Stop CLient
        private void button5_Click(object sender, EventArgs e)
        {
            client.Stop();
        }

        // Send Data
        private void button4_Click(object sender, EventArgs e)
        {
            byte[] data = null;
            try
            {
                data = stringToBytes(textBox2.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK);
            }
            finally
            {
                if (!client.Send(data))
                {
                    MessageBox.Show("Check inputdata", "Error", MessageBoxButtons.OK);
                }
            }
        }
    }
}
