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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Client_Ava
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<ListBoxItem> ChatList = new ObservableCollection<ListBoxItem>();
        private AdvancedTcpClient Client = new AdvancedTcpClient();
        private bool ShowError = true;
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
                    if (e.Exception != null && ShowError)
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
                    ShowError = false;
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
                    case 9:
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

                    // ���Ӵ���
                    case 255:
                        switch (args.ReceivedData[1])
                        {
                            // �û������������
                            case 0:
                                ShowError = false;
                                ContentDialog dialog = new ContentDialog
                                {
                                    Content = "����ʧ�ܣ��û������������",
                                    Title = "����ʧ��",
                                    CloseButtonText = "ȷ��",
                                    DefaultButton = ContentDialogButton.Close
                                };
                                dialog.ShowAsync();
                                break;

                            // �������ڲ�����
                            case 255:
                                ShowError = false;
                                ContentDialog dialog1 = new ContentDialog
                                {
                                    Content = "����ʧ�ܣ��������ڲ�����",
                                    Title = "����ʧ��",
                                    CloseButtonText = "ȷ��",
                                    DefaultButton = ContentDialogButton.Close
                                };
                                dialog1.ShowAsync();
                                break;
                        }
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
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ChatList.Clear();
                            ChatListBox.Items = ChatList;
                            InfoPage.Notices.Clear();
                        }).Wait();

                        Client.Connect(ip);
                        Client.BeginReceive();
                        string passwd_sha256 = GetSHA256(LoginPage.Password.Text);
                        Client.SendBytes(new byte[1] { 0 }.Concat(Encoding.UTF8.GetBytes(LoginPage.Username.Text + '^' + passwd_sha256)).ToArray());

                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            SendTextBox.IsEnabled = true;
                            SendButton.IsEnabled = true;
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
                long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                Client.Send($"{LoginPage.Username.Text} ˵��{SendTextBox.Text}");
                long timestamp2 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                ChatList.Add(new ListBoxItem
                {
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    IsHitTestVisible = false,
                    Content = new TextBlock { Text = $"��˵��{SendTextBox.Text}", TextWrapping = Avalonia.Media.TextWrapping.Wrap }
                });
                SendTextBox.Text = "";
            }
        }

        private string GetSHA256(string content)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] sha256_result = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
                StringBuilder sb = new StringBuilder();
                foreach (byte c in sha256_result)
                {
                    sb.Append(c.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }

    
}
