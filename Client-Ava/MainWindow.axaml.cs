using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Client_Ava.Pages;
using FluentAvalonia.UI.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Client_Ava
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<ListBoxItem> ChatList = new ObservableCollection<ListBoxItem>();
        private AdvancedTcpClient Client = new AdvancedTcpClient();
        private LoginPage LoginPage;
        private InfoPage InfoPage = new InfoPage();

        public MainWindow()
        {
            InitializeComponent();

            LoginPage = (LoginPage)Login.Content;
            LoginPage.MainWindow = this;
            InfoPage.MainWindow = this;
            Client.DataReceived += DataReceivedCallback;
            Client.Disconnected += (s, e) =>
            {
                Task.Run(() =>
                {
                    if (e.Exception != null)
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ContentDialog dialog = new ContentDialog
                            {
                                CloseButtonText = "ȷ��",
                                DefaultButton = ContentDialogButton.Close,
                                Title = "����",
                                Content = $@"�����쳣��ֹ��������Ϣ��{e.Exception.Message}"
                            };
                            dialog.ShowAsync();
                        });
                        Task.Delay(200).Wait();
                    }
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SendTextBox.IsEnabled = false;
                        SendButton.IsEnabled = false;
                        ChatList.Clear();
                        Login.IsHitTestVisible = false;
                        OpacityAnimation(Login, 0, TimeSpan.FromMilliseconds(300));
                    });
                    Task.Delay(300).Wait();

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Login.Content = LoginPage;
                        OpacityAnimation(Login, 1, TimeSpan.FromMilliseconds(300));
                        Login.IsHitTestVisible = true;
                    });
                });
            };
            Client.PingReceived += (s, e) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    InfoPage.PingText.Text = $"�ӳ٣�{e.Ping} ms";
                });
            };
        }

        private void OpacityAnimation(Animatable control, double opacity, TimeSpan duration)
        {
            Animation animation = new Animation
            {
                Duration = duration,
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
            animation.RunAsync(Login, null);
        }

        private void DataReceivedCallback(object? sender, AdvancedTcpClient.DataReceivedEventArgs args)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                switch (args.ReceivedData[0])
                {
                    // ����
                    case 0:
                        string notice = Encoding.UTF8.GetString(args.ReceivedData, 1, args.ReceivedData.Length - 1);
                        InfoPage.Notices.Add(new ListBoxItem { FontSize = 20, Content = notice, IsHitTestVisible = false });
                        break;

                    // ��Ϣ
                    case 1:
                        string message = Encoding.UTF8.GetString(args.ReceivedData, 1, args.ReceivedData.Length - 1);
                        ChatList.Add(new ListBoxItem
                        {
                            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                            Content = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            IsHitTestVisible = false
                        });
                        break;

                    case 255:
                        string stamp = Encoding.UTF8.GetString(args.ReceivedData, 1, args.ReceivedData.Length - 1);
                        ChatList.Add(new ListBoxItem
                        {
                            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                            Content = new TextBlock { Text = stamp, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            IsHitTestVisible = false
                        });
                        Client.Send($"{LoginPage.Username.Text} ˵��{stamp}");
                        break;
                }
            });
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
                Login.IsHitTestVisible = false;
                OpacityAnimation(Login, 0, TimeSpan.FromMilliseconds(300));
                Task.Run(() =>
                {
                    Task.Delay(500).Wait();

                    try
                    {
                        Client.Connect(ip);
                        Client.BeginReceive();
                        Client.SendBytes(new byte[1] { 0 }.Concat(Encoding.UTF8.GetBytes(LoginPage.Username.Text)).ToArray());

                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ChatList.Clear();
                            ChatListBox.Items = ChatList;
                            SendTextBox.IsEnabled = true;
                            SendButton.IsEnabled = true;
                            InfoPage.Notices.Clear();
                        }).Wait();
                    }
                    catch
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ContentDialog dialog = new ContentDialog
                            {
                                CloseButtonText = "ȷ��",
                                DefaultButton = ContentDialogButton.Close,
                                Content = "�޷����ӵ�������",
                                Title = "����"
                            };
                            dialog.ShowAsync();

                            SendTextBox.IsEnabled = false;
                            SendButton.IsEnabled = false;
                            OpacityAnimation(Login, 1, TimeSpan.FromMilliseconds(300));
                            Login.IsHitTestVisible = true;
                        });
                        return;
                    }

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        InfoPage.Username.Text = $"�û�����{LoginPage.Username.Text}";
                        var selectedItem = (FluentAvalonia.UI.Controls.ComboBoxItem)LoginPage.ServerSelectionComboBox.SelectedItem;
                        InfoPage.ServerName.Text = $"��������{selectedItem.Content}";
                        Login.Content = InfoPage;

                        OpacityAnimation(Login, 1, TimeSpan.FromMilliseconds(300));
                        Login.IsHitTestVisible = true;
                    });
                });
            }
        }

        public void Disconnect()
        {
            if (Client.Connected)
            {
                Client.Disconnect();
            }
        }

        private void SendButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SendTextBox.Text) || SendTextBox.Text.Length >= 2048)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "��ʾ",
                    Content = "������Ϣ����Ϊ�ջ򳬹� 2048 ����",
                    CloseButtonText = "ȷ��",
                    DefaultButton = ContentDialogButton.Close
                };
                dialog.ShowAsync();
            }
            else
            {
                Client.Send($"{LoginPage.Username.Text} ˵��{SendTextBox.Text}");
                ChatList.Add(new ListBoxItem
                {
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Content = new TextBlock { Text = $"��˵��{SendTextBox.Text}", TextWrapping = Avalonia.Media.TextWrapping.Wrap }
                });
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
        }

        public class PingReceivedEventArgs : EventArgs
        {
            public int Ping { get; set; }
        }

        public class DisconnectedEventArgs : EventArgs
        {
            public Exception Exception { get; init; }
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

        public event EventHandler<PingReceivedEventArgs> PingReceived = delegate { };

        public event EventHandler<DisconnectedEventArgs> Disconnected = delegate { };

        public AdvancedTcpClient() { }

        public void Connect(string ip)
        {
            client?.Close();
            Task.Delay(10).Wait();
            client = new TcpClient();
            int idx = ip.LastIndexOf(':');
            string ip1 = ip[..idx];
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
                    long timediff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    while (true)
                    {
                        try
                        {
                            // ����
                            int size = 0;
                            byte[] buffer = new byte[8193];
                            if (client.Client != null)
                            {
                                size = client.Client.Receive(buffer);
                                Array.Resize(ref buffer, size);
                            }
                            else
                            {
                                Connected = false;
                                break;
                            }
                            if (size <= 0)
                            {
                                throw new SocketException(10054);
                            }
                            if (buffer[0] == 253)
                            {
                                timediff = BitConverter.ToInt64(buffer, 1);
                            }
                            else if (buffer[0] == 254)
                            {
                                long timestamp = BitConverter.ToInt64(buffer, 1);
                                PingReceived(client, new PingReceivedEventArgs { Ping = (int)(timestamp - timediff - 500) });
                            }
                            else
                            {
                                DataReceived(
                                    client, new DataReceivedEventArgs { ReceivedData = buffer });
                            }
                        }
                        catch (Exception ex)
                        {
                            if (Connected)
                            {
                                Connected = false;
                                client.Close();
                                Disconnected(this, new DisconnectedEventArgs { Exception = ex });
                            }
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
            return SendBytes(new byte[1] { 1 }.Concat(Encoding.UTF8.GetBytes(message)).ToArray());
        }

        public bool SendBytes(byte[] data)
        {
            if (Connected)
            {
                try
                {
                    client.Client.Send(data);
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
            if (Connected)
            {
                Connected = false;
                client?.Close();
                Disconnected(this, new DisconnectedEventArgs());
            }
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
