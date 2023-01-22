using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Client_Ava.Pages;
using FluentAvalonia.UI.Controls;
using System;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Client_Ava
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<string> ChatList = new ObservableCollection<string>();
        private AdvancedTcpClient Client = new AdvancedTcpClient();
        private LoginPage LoginPage = new LoginPage();

        public MainWindow()
        {
            InitializeComponent();

            LoginPage.MainWindow = this;
            Login.Content = LoginPage;
        }

        public void Connect(string username, string ip)
        {
            if (string.IsNullOrEmpty(username) || username.Length >= 12)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "��ʾ",
                    Content = "�û�������Ϊ�ջ���� 12 �ַ�",
                    CloseButtonText = "ȷ��",
                    DefaultButton = ContentDialogButton.Close
                };
                dialog.ShowAsync();
            }
            else
            {
                double opacity = 0;
                Animation animation = new Animation
                {
                    Duration = TimeSpan.FromSeconds(0.5),
                    PlaybackDirection = PlaybackDirection.Normal,
                    FillMode = FillMode.Both
                };
                var kf = new KeyFrame
                {
                    Cue = new Cue(1.0)
                };
                kf.Setters.Add(new Setter
                {
                    Property = OpacityProperty,
                    Value = opacity
                });
                animation.Children.Add(kf);

                Login.IsEnabled = false;
                animation.RunAsync(Login, null);
                ChatList.Clear();
                ChatListBox.Items = ChatList;
                SendTextBox.IsEnabled = true;
                accent_button.IsEnabled = true;
                accent_button.IsDefault = true;

                Client.Connect(ip);
                Client.BeginReceive();
                Client.DataReceived += (s, e) =>
                {
                    string message = Encoding.UTF8.GetString(e.ReceivedData, 0, e.size);
                    ChatList.Add(message);
                };
            }
        }

        private void SendButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SendTextBox.Text) || SendTextBox.Text.Length >= 240)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "��ʾ",
                    Content = "������Ϣ����Ϊ�ջ򳬹� 240 ����",
                    CloseButtonText = "ȷ��",
                    DefaultButton = ContentDialogButton.Close
                };
                dialog.ShowAsync();
            }
            else
            {
                Client.Send($"{LoginPage.Username.Text} ˵��{SendTextBox.Text}");
                SendTextBox.Text = "";
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
            Task.Delay(10).Wait();
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
                                client, new DataReceivedEventArgs { ReceivedData = buffer, size = size });
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
