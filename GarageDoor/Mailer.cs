using System;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace GarageDoor
{
    public class Mailer : IDisposable
    {
        private StreamSocket m_clientSocket = new StreamSocket();
        private HostName m_serverHost;
        private bool m_connected = false;

        private const string SERVER_HOST_NAME = "shootingstar2";
        private const string SERVER_PORT_NUMBER = "25";

        public void Connect()
        {
            if (m_connected) return;

            try
            {
                m_serverHost = new HostName(SERVER_HOST_NAME);
                IAsyncAction taskLoad = m_clientSocket.ConnectAsync(m_serverHost, SERVER_PORT_NUMBER);
                taskLoad.AsTask().Wait();
                m_connected = true;
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

        public void Send(string data)
        {
            if (!m_connected) return;

            try
            {
                SendSMTPCommands(data);
            }
            catch (Exception exception)
            {
                // If this is an unknown status, 
                // it means that the error is fatal and retry will likely fail.
                if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                    throw;

                // Could retry the connection, but for this simple example
                // just close the socket.

                m_connected = false;
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

                WriteData(writer, "HELO shootingstarbbs.us");
                rxData = ReadData(reader); // 250

                WriteData(writer, "MAIL FROM:<diskcrasher@gmail.com>");
                rxData = ReadData(reader); // 250

                WriteData(writer, "RCPT TO:<diskcrasher@gmail.com>");
                rxData = ReadData(reader); // 250

                WriteData(writer, "DATA");
                rxData = ReadData(reader); // 250

                // add a newline to the text to send
                string txData = "From: Raspberry Pi3 <admin@shootingstarbbs.us>\n";
                txData += "To: diskcrasher@gmail.com\n";
                txData += $"Date: {DateTime.Now}\n";
                txData += "Subject: Garage door is OPEN!\n\n";
                txData += data + Environment.NewLine + ".";
                WriteData(writer, txData);
                rxData = ReadData(reader); // 250

                WriteData(writer, "QUIT");
                rxData = ReadData(reader); // 221

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
            if (!txData.EndsWith("\n")) txData += "\n";
            uint len = writer.MeasureString(txData);
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
