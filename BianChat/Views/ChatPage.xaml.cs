﻿using BianChat.Controls;
using BianChat.Models;
using BianChat.Tools;
using BianChat.Views.Dialog;
using ModernWpf.Controls;
using ModernWpf.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BianChat.Views
{
    /// <summary>
    /// ChatPage.xaml 的交互逻辑
    /// </summary>
    public partial class ChatPage : System.Windows.Controls.Page
    {
        private ObservableCollection<UserListItem> userList = new ObservableCollection<UserListItem>();

        public ChatPage()
        {
            InitializeComponent();

            Loaded += delegate
            {
                UserListBox.ItemsSource = userList;
                if (!AccountProfile.IsLogin)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "提示",
                        Content = "请到登录页进行登录。",
                        PrimaryButtonText = "跳转至登录页",
                        DefaultButton = ContentDialogButton.Primary
                    };
                    dialog.Closing += delegate
                    {
                        PublicValues.MainWindow.RootNavigation.SelectedItem = PublicValues.MainWindow.Account;
                    };
                    DialogTools.ShowDialog(dialog);
                    return;
                }
                AccountProfile.Current.Client.Disconnected += delegate
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageTextBox.IsEnabled = false;
                        SendButton.IsEnabled = false;
                    });
                };
                AccountProfile.Current.Client.ProfileChanged += (s, e) =>
                {
                    UpdateFriendList();
                    if (e)
                    {
                        DialogTools.ShowDialogWithCloseButton("提示", "用户档案修改成功");
                    }
                    else
                    {
                        DialogTools.ShowDialogWithCloseButton("错误", "用户档案修改失败");
                    }
                };
                AccountProfile.Current.Client.MessageSent += (s, e) =>
                {
                    switch (e)
                    {
                        case 0: // 发送成功
                            MessageTextBox.IsEnabled = true;
                            SendButton.IsEnabled = true;
                            break;
                        case 1: // 对方不在线
                            DialogTools.ShowDialogWithCloseButton("提示", "对方不在线，无法发送消息");
                            break;
                    }
                };
                UpdateFriendList();
            };
        }

        private void UpdateFriendList()
        {
            foreach (var user in AccountProfile.Current.UserInfo.FriendList)
            {
                Dispatcher.Invoke(() =>
                {
                    userList.Add(new UserListItem { Username = user.Username, LastMessage = "6", Tag = user.UID });
                });
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            MessageTextBox.IsEnabled = false;
            SendButton.IsEnabled = false;

            UserListItem user = (UserListItem)UserListBox.SelectedItem;
            int uid = (int)user.Tag;
            AccountProfile.Current.Client.SendMessage(uid, MessageTextBox.Text);
        }

        private void UserListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MessageTextBox.IsEnabled = true;
            SendButton.IsEnabled = true;
        }

        private void AddFriendButton_Click(object sender, RoutedEventArgs e)
        {
            if (AccountProfile.IsLogin)
            {
                AddFriendDialog page = new AddFriendDialog();
                ContentDialog dialog = new ContentDialog
                {
                    Title = "添加好友",
                    Content = page,
                    PrimaryButtonText = "添加",
                    PrimaryButtonCommand = new CommandModel((obj) => { return true; }, (obj) =>
                    {
                        int uid = int.Parse(page.UIDTextBox.Text);
                        UserInfo friend = AccountProfile.Current.GetUserInfo(uid);
                        List<UserInfo> friends = AccountProfile.Current.UserInfo.FriendList.ToList();
                        friends.Add(friend);
                        Task.Run(() => AccountProfile.Current.EditFriendList(friends));
                    }),
                    DefaultButton = ContentDialogButton.Primary
                };
                DialogTools.ShowDialog(dialog);
            }
        }
    }
}
