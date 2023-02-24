﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BianChat
{
    public class AccountProfile
    {
        public static AccountProfile Current { get; private set; }
        public static bool Connected { get; private set; } = false;
        public string Username { get; private set; }
        public string ProfilePhotoUrl { get; private set; }
        public AccountProfile[] FriendList { get; private set; }
        public ChatClient Client { get; }

        public AccountProfile(string username, string password)
        {
            Client = new ChatClient();
            Client.client.Disconnected += delegate
            {
                Connected = false;
            };
            Current?.Client.Disconnect();
            Current = new AccountProfile();
            Current.Client.Connect(username, password);
            Current.Username = username;
            Connected = true;
        }
    }
}
