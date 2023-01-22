using System.Drawing;
using Avalonia.Controls;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using System.Collections.ObjectModel;
using Avalonia.Layout;

namespace Client_Ava
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<string> ChatList = new ObservableCollection<string>();
        private AdvancedTcpClient Client = new AdvancedTcpClient();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ConnectButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (UserName.Text == "" || UserName.Text.Length == 0 || UserName.Text.Length >= 12)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "��ʾ",
                    Content = "�û�����Ϊ�ջ����12�ַ�",
                    CloseButtonText = "ȷ��",
                    DefaultButton = ContentDialogButton.Close
                };
                dialog.ShowAsync();
                return;
            }
            else
            {
                Login.IsVisible = false;
                ChatList.Clear();
                ChatListBox.Items = ChatList;

                FluentAvalonia.UI.Controls.ComboBoxItem item = ServerSelectionComboBox.SelectedItem as FluentAvalonia.UI.Controls.ComboBoxItem;
                string ip = item.Tag as string;
                Client.Connect(ip);
                Client.BeginReceive();
                Client.DataReceived += (s,e) =>
                {
                    string message = Encoding.UTF8.GetString(e.ReceivedData,0,e.size);
                    ChatList.Add(message);
                };
            }

        }
    }

    public class AdvancedTcpClient : IDisposable
    {
        // EventArgs
        public class DataReceivedEventArgs : EventArgs
        {
            public byte[] ReceivedData { get; set; }
            public int size { get; set; }
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

        public AdvancedTcpClient() { }

        public void Connect(string ip)
        {
            client?.Close();
            client = new TcpClient();
            int idx = ip.LastIndexOf(':');
            string ip1 = ip[..(idx)];
            int port = int.Parse(ip[(idx + 1)..]);
            client.Connect(ip1, port);
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
                            int size = 0;
                            byte[] buffer = new byte[512];
                            if (client.Client != null)
                            {
                                size = client.Client.Receive(buffer);
                            }
                            else
                            {
                                Connected = false;
                                break;
                            }
                            DataReceived(
                                client,new DataReceivedEventArgs { ReceivedData = buffer,size = size });
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

        public void Disconnect()
        {
            client?.Close();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: �ͷ��й�״̬(�йܶ���)
                    Disconnect();
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
