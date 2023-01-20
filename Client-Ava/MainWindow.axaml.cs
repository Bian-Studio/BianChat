using Avalonia.Controls;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

namespace Client_Ava
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }

    public class AdvancedTcpClient : IDisposable
    {
        // EventArgs
        public class DataReceivedEventArgs : EventArgs
        {
            public byte[] ReceivedData { get; set; } = new byte[0];
        }

        // TCP �ͻ���
        private TcpClient client;

        /// <summary>
        /// �����̣߳�����
        /// </summary>
        public Thread ReceiveTask;
        /// <summary>
        /// �Ƿ�����
        /// </summary>
        public bool Connected = false;
        private bool disposedValue;

        /// <summary>
        /// ���ݽ����¼�
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceived = delegate { };

        public AdvancedTcpClient()
        {
            client = new TcpClient();
        }

        public void Connect(string ip, int port)
        {
            client.Connect(ip, port);
            Connected = true;
        }

        public void BeginReceive()
        {
            if (Connected)
            {
                ReceiveTask = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            // ����
                            byte[] buffer = new byte[512];
                            if (client.Client != null)
                            {
                                client.Client.Receive(buffer);
                            }
                            else
                            {
                                Connected = false;
                                break;
                            }
                            DataReceived(
                                client, new DataReceivedEventArgs { ReceivedData = buffer });
                        }
                        catch
                        {
                            client.Close();
                            Connected = false;
                            break;
                        }
                    }
                });
                ReceiveTask.IsBackground = true;
                ReceiveTask.Start();
            }
        }

        public bool Send(string message)
        {
            if (Connected)
            {
                try
                {
                    client.Client.Send(Encoding.UTF8.GetBytes(message));
                    return true;
                }
                catch
                {
                    Connected = false;
                    return false;
                }
            }

            return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: �ͷ��й�״̬(�йܶ���)
                    client.Close();
                }

                // TODO: �ͷ�δ�йܵ���Դ(δ�йܵĶ���)����д�ս���
                // TODO: �������ֶ�����Ϊ null
                disposedValue = true;
            }
        }

        // // TODO: ������Dispose(bool disposing)��ӵ�������ͷ�δ�й���Դ�Ĵ���ʱ������ս���
        // ~AdvancedTcpClient()
        // {
        //     // ��Ҫ���Ĵ˴��롣�뽫���������롰Dispose(bool disposing)��������
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // ��Ҫ���Ĵ˴��롣�뽫���������롰Dispose(bool disposing)��������
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
