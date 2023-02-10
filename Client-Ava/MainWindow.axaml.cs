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
        private UserControl PanePage;
        private PageType PanePageType;
        private LoginPage LoginPage = new LoginPage();
        private InfoPage InfoPage = new InfoPage();
        private RegisterPage RegisterPage = new RegisterPage();

        public MainWindow()
        {
            InitializeComponent();

            LoginPage.MainWindow = this;
            InfoPage.MainWindow = this;
            RegisterPage.MainWindow = this;
            PanePage = LoginPage;
            PanePageType = PageType.LoginPage;
            Login.Content = PanePage;

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
                    });
                    SwitchPage(PageType.LoginPage);
                });
            };
            Client.PingReceived += (s, e) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    InfoPage.PingText.Text = $"�ӳ٣�{e.Ping} ms";
                });
            };

            LoginSuccessEvent += delegate
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SendTextBox.IsEnabled = true;
                    SendButton.IsEnabled = true;
                }).Wait();

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    InfoPage.Username.Text = $"�û�����{LoginPage.Username.Text}";
                    var selectedItem = LoginPage.ServerSelectionComboBox.SelectedItem as Avalonia.Controls.ComboBoxItem;
                    InfoPage.ServerName.Text = $"��������{selectedItem?.Content}";
                }).Wait();

                SwitchPage(PageType.InfoPage);
            };
        }

        public void SwitchPage(PageType type)
        {
            Task.Run(() =>
            {
                PanePageType = type;
                switch (type)
                {
                    case PageType.LoginPage:
                        PanePage = LoginPage;
                        break;
                    case PageType.InfoPage:
                        PanePage = InfoPage;
                        break;
                    case PageType.RegisterPage:
                        PanePage = RegisterPage;
                        break;
                }
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Login.IsHitTestVisible = false;
                    OpacityAnimation(Login, 0, TimeSpan.FromMilliseconds(300));
                });
                Task.Delay(300).Wait();

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Login.Content = PanePage;
                    OpacityAnimation(Login, 1, TimeSpan.FromMilliseconds(300));
                    Login.IsHitTestVisible = true;
                });
            });
        }

        public enum PageType
        {
            LoginPage,
            InfoPage,
            RegisterPage
        }

        private void DataReceivedCallback(object? sender, AdvancedTcpClient.DataReceivedEventArgs args)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                switch (args?.ReceivedData?[0])
                {
                    // ����
                    case 6:
                        string notice = Encoding.UTF8.GetString(args.ReceivedData, 1, args.ReceivedData.Length - 1);
                        InfoPage.Notices.Add(new ListBoxItem { FontSize = 20, Content = notice, IsHitTestVisible = false });
                        break;

                    // ��Ϣ
                    case 9:
                        string message = Encoding.UTF8.GetString(args.ReceivedData, 1, args.ReceivedData.Length - 1);
                        ChatList.Add(new ListBoxItem
                        {
                            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                            Content = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            IsHitTestVisible = false
                        });
                        break;
                    // ��¼�ɹ�
                    case 2:
                        LoginSuccessEvent(null, new EventArgs());
                        break;
                    // �������ڲ�����
                    case 4:
                        ShowError = false;
                        new ContentDialog
                        {
                            Content = "����ʧ�ܣ��������ڲ�����",
                            Title = "����ʧ��",
                            CloseButtonText = "ȷ��",
                            DefaultButton = ContentDialogButton.Close
                        }.ShowAsync();
                        break;
                    // �û������������
                    case 5:
                        ShowError = false;
                        new ContentDialog
                        {
                            Content = "����ʧ�ܣ��û������������",
                            Title = "����ʧ��",
                            CloseButtonText = "ȷ��",
                            DefaultButton = ContentDialogButton.Close
                        }.ShowAsync();
                        break;
                }
            });
        }

        private event EventHandler LoginSuccessEvent = delegate { };

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
                        Client.SendBytes(new byte[1] { 7 }.Concat(Encoding.UTF8.GetBytes(LoginPage.Username.Text + '^' + passwd_sha256)).ToArray());
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ContentDialog dialog = new ContentDialog
                            {
                                CloseButtonText = "ȷ��",
                                DefaultButton = ContentDialogButton.Close,
                                Content = $"�޷����ӵ���������{ex.Message}",
                                Title = "����"
                            };
                            dialog.ShowAsync();

                            SendTextBox.IsEnabled = false;
                            SendButton.IsEnabled = false;
                        }).Wait();

                        SwitchPage(PageType.LoginPage);
                        return;
                    }
                });
            }
        }

        public void Register(string username, string password)
        {
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
    }
}
