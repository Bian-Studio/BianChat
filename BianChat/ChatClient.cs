﻿using BianChat.DataType.Packet;
using BianChat.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BianChat
{
    public class ChatClient
    {
        public class MessageReceivedEventArgs : EventArgs
        {
            public string Username { get; set; }
            public string Message { get; set; }
            public AdvancedTcpClient.PacketType MessageType { get; set; }
        }

        public class LoginCompletedEventArgs : EventArgs
        {
            public State LoginState { get; set; }
            public UserInfo UserInfo { get; set; }

            public enum State
            {
                Success,
                Failed_Account,
                Failed_Unknown
            }
        }

        public AdvancedTcpClient client;
        public bool Connected = false;
        public bool IsLogin = false;
        public UserInfo UserInfo;

        public event EventHandler<AdvancedTcpClient.PingReceivedEventArgs> PingReceived = delegate { };
        public event EventHandler<LoginCompletedEventArgs> LoginCompleted = delegate { };
        public event EventHandler<MessageReceivedEventArgs> MessageReceived = delegate { };
        public event EventHandler<int> MessageSent = delegate { };
        public event EventHandler<int> ProfileChanged = delegate { };
        public event EventHandler<int> AddFriendCompleted = delegate { };
        public event EventHandler<AdvancedTcpClient.DisconnectedEventArgs> Disconnected = delegate { };

        public ChatClient() { }

        public void Connect(string username, string password, string ip = "127.0.0.1:911")
        {
            // 断开已有连接
            client = new AdvancedTcpClient();
            client.PingReceived += (s, e) => { PingReceived(s, e); };
            client.DataReceived += DataReceivedCallback;
            client.Disconnected += (s, e) =>
            {
                IsLogin = false;
                Connected = false;

                Task.Run(() =>
                {
                    Disconnected(s, e);
                });
            };
            try
            {
                client.Connect(ip);
                Connected = true;
            }
            catch (Exception ex)
            {
                Connected = false;
                Task.Run(() =>
                {
                    Disconnected(null, new AdvancedTcpClient.DisconnectedEventArgs { Exception = ex });
                });
            }

            // 登录
            string passwdHash = HashTools.GetSHA256(password);
            StringCustomPacket packet = new StringCustomPacket { PacketType = AdvancedTcpClient.PacketType.Login };
            packet.DataList.Add("0");
            packet.DataList.Add(username);
            packet.DataList.Add(passwdHash);
            client.SendBytes(packet.GetBytes());
        }

        private void DataReceivedCallback(object sender, AdvancedTcpClient.DataReceivedEventArgs e)
        {
            StringCustomPacket pack = new StringCustomPacket();
            switch (e.DataType)
            {
                // 登录成功
                case AdvancedTcpClient.PacketType.State_Account_Success:
                    IsLogin = true;
                    string[] account_info = Encoding.UTF8.GetString(e.ReceivedData).Split('|');
                    int uid = int.Parse(account_info[0]);
                    string username = account_info[1];
                    string email = account_info[2];
                    string[] friendsUID = account_info[3].Split('^');
                    List<UserInfo> friends = new List<UserInfo>();

                    // 获取好友信息
                    foreach (string friend in friendsUID)
                    {
                        if (friend == "") continue;
                        int friendUid = int.Parse(friend);
                        friends.Add(GetAccountInfo(friendUid));
                    }
                    UserInfo = new UserInfo
                    {
                        UID = uid,
                        Username = username,
                        Email = email,
                        FriendList = friends.ToArray()
                    };

                    Task.Run(() =>
                    {
                        LoginCompleted(null, new LoginCompletedEventArgs
                        {
                            LoginState = LoginCompletedEventArgs.State.Success,
                            UserInfo = UserInfo
                        });
                    });
                    break;

                // 登录失败——账号或密码错误
                case AdvancedTcpClient.PacketType.Error_Account:
                    Task.Run(() =>
                    {
                        LoginCompleted(null, new LoginCompletedEventArgs { LoginState = LoginCompletedEventArgs.State.Failed_Account });
                    });
                    break;

                // 登录失败——未知错误
                case AdvancedTcpClient.PacketType.Error_Unknown:
                    Task.Run(() =>
                    {
                        LoginCompleted(null, new LoginCompletedEventArgs { LoginState = LoginCompletedEventArgs.State.Failed_Unknown });
                    });
                    break;

                // 接收到消息
                case AdvancedTcpClient.PacketType.Message:
                    Task.Run(() =>
                    {
                        string[] message_info = pack.GetDatas(e.ReceivedData);
                        MessageReceived(null, new MessageReceivedEventArgs
                        {
                            Username = message_info[0],
                            Message = message_info[1],
                            MessageType = AdvancedTcpClient.PacketType.Message
                        });
                    });
                    break;
                // 接收到公告
                case AdvancedTcpClient.PacketType.Notice:
                    Task.Run(() =>
                    {
                        MessageReceived(null, new MessageReceivedEventArgs
                        {
                            Message = Encoding.UTF8.GetString(e.ReceivedData),
                            MessageType = AdvancedTcpClient.PacketType.Notice
                        });
                    });
                    break;
                // 用户档案信息更改
                case AdvancedTcpClient.PacketType.Update_Value_Result:
                    Task.Run(() =>
                    {
                        ProfileChanged(null, e.ReceivedData[0]);
                    });
                    break;
                // 消息发送完成
                case AdvancedTcpClient.PacketType.Message_Send_Status:
                    Task.Run(() =>
                    {
                        MessageSent(null, e.ReceivedData[0]);
                    });
                    break;
                // 添加好友完成
                case AdvancedTcpClient.PacketType.Add_Friend_Result:
                    Task.Run(() =>
                    {
                        AddFriendCompleted(null, e.ReceivedData[0]);
                    });
                    break;
            }
        }

        public void SendMessage(int uid, string message)
        {
            StringCustomPacket pack = new StringCustomPacket { PacketType = AdvancedTcpClient.PacketType.Message };
            pack.DataList.Add(uid.ToString());
            pack.DataList.Add(message);
            client.SendBytes(pack.GetBytes());
        }

        public string GetValue(ValuesType type)
        {
            string result = null;
            client.DataReceived += (s, e) =>
            {
                if (e.DataType == AdvancedTcpClient.PacketType.Get_Value_Result)
                {
                    if (e.ReceivedData.Length == 1)
                    {
                        result = "";
                        return;
                    }
                    result = Encoding.UTF8.GetString(e.ReceivedData, 1, e.ReceivedData.Length - 1);
                }
            };
            client.SendPacket(AdvancedTcpClient.PacketType.Get_Value, new byte[1] { (byte)type });
            while (result == null) Task.Delay(10).Wait();
            return result;
        }

        public UserInfo GetAccountInfo(int uid)
        {
            UserInfo userInfo = null;
            client.DataReceived += (s, e) =>
            {
                if (e.DataType == AdvancedTcpClient.PacketType.Get_Account_Info_Result)
                {
                    string[] infos = Encoding.UTF8.GetString(e.ReceivedData, 4, e.ReceivedData.Length - 4).Split('^');
                    UserInfo info1 = new UserInfo();
                    info1.UID = BitConverter.ToInt32(e.ReceivedData);
                    if (e.ReceivedData.Length == 1) throw new ArgumentException($"找不到 UID 为 {info1.UID} 用户");
                    info1.Username = infos[0];
                    userInfo = info1;
                }
            };
            bool result = client.SendPacket(AdvancedTcpClient.PacketType.Get_Account_Info, BitConverter.GetBytes(uid));
            while (userInfo == null) Task.Delay(10).Wait();
            return userInfo;
        }

        public void ChangeProfile(ValuesType type, string value)
            => client.SendPacket(AdvancedTcpClient.PacketType.Update_Value,
                new byte[1] { (byte)type }.Concat(Encoding.UTF8.GetBytes(value)).ToArray());

        public void AddFriend(int uid)
            => client.SendPacket(AdvancedTcpClient.PacketType.Add_Friend,
                BitConverter.GetBytes(uid));

        public enum ValuesType
        {
            Username,
            Password,
            ProfilePhoto,
            FriendList,
            UnprocessedRequests,
            Email
        }

        public enum RequestType
        {
            From_Add_Friend = 0,
            To_Add_Friend = 1
        }

        public void Disconnect()
        {
            client.Dispose();
        }
    }
}
