/*
MIT License

Copyright (c) 2017 Michael J. Lowery

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace GarageDoor
{
    /// <summary>
    /// This class contains methods for communicating with an SMTP server.
    /// </summary>
    public class Mailer : IDisposable
    {
        private const string SERVER_HOST_NAME = "10.0.0.2";
        private const string SERVER_PORT_NUMBER = "25";
        private const string FROM_EMAIL = "diskcrasher@gmail.com";
        private const string TO_EMAIL = "diskcrasher@gmail.com";
        private const string ADMIN_EMAIL = "admin@shootingstarbbs.us";

        private StreamSocket m_clientSocket = new StreamSocket();
        private HostName m_serverHost;
        private bool m_socketConnected = false;

        /// <summary>
        /// Attempts to connect to the SMTP server.
        /// </summary>
        public void Connect()
        {
            if (m_socketConnected) return;

            try
            {
                m_serverHost = new HostName(SERVER_HOST_NAME);
                IAsyncAction taskLoad = m_clientSocket.ConnectAsync(m_serverHost, SERVER_PORT_NUMBER);
                taskLoad.AsTask().Wait();
                m_socketConnected = true;
            }
            catch (Exception exception)
            {
                // If this is an unknown status, 
                // it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                    throw;

                // Could retry the connection, but for this simple example
                // just close the socket.
            }
        }

        /// <summary>
        /// Send data/command to the SMTP server.
        /// </summary>
        /// <param name="data"></param>
        public void Send(string doorStatus, string data)
        {
            if (!m_socketConnected) return;

            try
            {
                SendSMTPCommands(doorStatus, data);
            }
            catch (Exception e)
            {
                // If this is an unknown status, 
                // it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(e.HResult) == SocketErrorStatus.Unknown)
                    throw;

                // Could retry the connection, but for this simple example
                // just close the socket.

                m_socketConnected = false;
            }
        }

        /// <summary>
        /// Sends e-mail message to SMTP server.
        /// </summary>
        /// <param name="data">Body of e-mail.</param>
        /// <seealso cref="http://www.samlogic.net/articles/smtp-commands-reference.htm"/>
        private void SendSMTPCommands(string doorStatus, string data)
        {
            using (DataWriter writer = new DataWriter(m_clientSocket.OutputStream))
            using (DataReader reader = new DataReader(m_clientSocket.InputStream))
            {
                // Set inputstream options so that we don't have to know the data size
                reader.InputStreamOptions = InputStreamOptions.Partial;

                string rxData = ReadData(reader); // Read 220 connection response message.
                if (!rxData.StartsWith("220")) throw new InvalidOperationException("Unexpected return result");

                WriteData(writer, "HELO shootingstarbbs.us");
                rxData = ReadData(reader); // 250
                if (!rxData.StartsWith("250")) throw new InvalidOperationException("Unexpected return result");

                WriteData(writer, $"MAIL FROM:<{FROM_EMAIL}>");
                rxData = ReadData(reader); // 250
                if (!rxData.StartsWith("250")) throw new InvalidOperationException("Unexpected return result");

                WriteData(writer, $"RCPT TO:<{TO_EMAIL}>");
                rxData = ReadData(reader); // 250
                if (!rxData.StartsWith("250")) throw new InvalidOperationException("Unexpected return result");

                WriteData(writer, "DATA");
                rxData = ReadData(reader); // 354
                if (!rxData.StartsWith("354")) throw new InvalidOperationException("Unexpected return result");

                // Add a newline to the text to send.
                string txData = $"From: Raspberry Pi3 <{ADMIN_EMAIL}>\n";
                txData += $"To: {TO_EMAIL}\n";
                txData += $"Date: {DateTime.Now}\n";
                txData += $"Subject: Garage door is {doorStatus}\n\n";
                txData += data + Environment.NewLine + ".";
                WriteData(writer, txData);
                rxData = ReadData(reader); // 250
                if (!rxData.StartsWith("250")) throw new InvalidOperationException();

                WriteData(writer, "QUIT");
                rxData = ReadData(reader); // 221
                if (!rxData.StartsWith("221")) throw new InvalidOperationException();

                // Detach the streams and close them.
                writer.DetachStream();
                reader.DetachStream();
            }
        }

        private static string ReadData(DataReader reader)
        {
            IAsyncOperation<uint> taskLoad = reader.LoadAsync(80);
            taskLoad.AsTask().Wait();
            uint bytesRead = taskLoad.GetResults();
            byte[] rxData = new byte[bytesRead];
            reader.ReadBytes(rxData);

            return System.Text.Encoding.UTF8.GetString(rxData);
        }

        private static void WriteData(DataWriter writer, string txData)
        {
            if (!txData.EndsWith(Environment.NewLine)) txData += Environment.NewLine;
            //uint len = writer.MeasureString(txData);
            writer.WriteString(txData);
            DataWriterStoreOperation writerLoad = writer.StoreAsync();
            writerLoad.AsTask().Wait();
            IAsyncOperation<bool> writerFlush = writer.FlushAsync();
            writerFlush.AsTask().Wait();
        }

        #region IDisposable Support
        private bool m_disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    if (m_clientSocket != null) m_clientSocket.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                m_disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Mailer() {
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
