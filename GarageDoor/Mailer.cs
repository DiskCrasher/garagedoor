﻿using System;
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
        private StreamSocket m_clientSocket = new StreamSocket();
        private HostName m_serverHost;
        private bool m_socketConnected = false;

        private const string SERVER_HOST_NAME = "10.0.0.2";
        private const string SERVER_PORT_NUMBER = "25";

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
        public void Send(string data)
        {
            if (!m_socketConnected) return;

            try
            {
                SendSMTPCommands(data);
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

        private void SendSMTPCommands(string data)
        {
            using (DataWriter writer = new DataWriter(m_clientSocket.OutputStream))
            using (DataReader reader = new DataReader(m_clientSocket.InputStream))
            {
                // Set inputstream options so that we don't have to know the data size
                reader.InputStreamOptions = InputStreamOptions.Partial;

                string rxData = ReadData(reader); // Read 220 connection response message.
                if (!rxData.StartsWith("220")) throw new InvalidOperationException();

                WriteData(writer, "HELO shootingstarbbs.us");
                rxData = ReadData(reader); // 250
                if (!rxData.StartsWith("250")) throw new InvalidOperationException();

                WriteData(writer, "MAIL FROM:<diskcrasher@gmail.com>");
                rxData = ReadData(reader); // 250
                if (!rxData.StartsWith("250")) throw new InvalidOperationException();

                WriteData(writer, "RCPT TO:<diskcrasher@gmail.com>");
                rxData = ReadData(reader); // 250
                if (!rxData.StartsWith("250")) throw new InvalidOperationException();

                WriteData(writer, "DATA");
                rxData = ReadData(reader); // 354
                if (!rxData.StartsWith("354")) throw new InvalidOperationException();

                // Add a newline to the text to send.
                string txData = "From: Raspberry Pi3 <admin@shootingstarbbs.us>\n";
                txData += "To: diskcrasher@gmail.com\n";
                txData += $"Date: {DateTime.Now}\n";
                txData += "Subject: Garage door is OPEN!\n\n";
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
