using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using System.Text.RegularExpressions;

namespace SimParticipate
{
    public enum ChatCommands
    {
        Echo,
        Failure,
        TransmitClientEvent
    }

    public class CommandEventArgs : EventArgs
    {
        public ChatCommands Command { get; private set; }
        public string PrimaryArgument { get; private set; }
        public string SecondaryArgument { get; private set; }

        public CommandEventArgs(ChatCommands cmd, string arg1, string arg2)
        {
            this.Command = cmd;
            this.PrimaryArgument = arg1.Trim();
            this.SecondaryArgument = arg2.Trim();
        }
    }

    class TwitchBot
    {
        TwitchClient client;
        private Log log;

        Regex command = new Regex(@"^!(\w+)(\s+(\w+))?(\s+(\w+))?");

        public EventHandler<CommandEventArgs> CommandEvent;

        protected virtual void OnRaiseCommandEvent(CommandEventArgs e)
        {
            CommandEvent?.Invoke(this, e);
        }

        public TwitchBot(string username, string token)
        {
            log = Log.Instance;
            
            var credentials = new ConnectionCredentials(username, token);
            client = new TwitchClient();
            client.Initialize(credentials, username);

            client.OnLog += OnLog;
            client.OnJoinedChannel += OnJoinedChannel;
            client.OnMessageReceived += OnMessageReceived;
            client.OnWhisperReceived += OnWhisperReceived;
            client.OnNewSubscriber += OnNewSubscriber;
            client.OnConnected += OnConnected;

            client.Connect();
        }

        private void OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            client.SendMessage(e.Channel, "Hey y'all!  This message is from a bot powered by TwitchLib!");
        }

        private void OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
        {
            if (e.WhisperMessage.Username == "my_friend")
            {
                client.SendWhisper(e.WhisperMessage.Username, "Psst!");
            }
        }

        private void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Message.StartsWith("!"))
            {
                var result = command.Match(e.ChatMessage.Message);
                var cmd = result.Groups[1];
                var arg1 = result.Groups[3];
                var arg2 = result.Groups[5];

                switch (cmd.ToString().ToLowerInvariant())
                {
                    case "echo":
                        client.SendMessage(e.ChatMessage.Channel, $"{e.ChatMessage.DisplayName} just said '{e.ChatMessage.Message}'");
                        OnRaiseCommandEvent(new CommandEventArgs(ChatCommands.Echo, arg1.ToString(), arg2.ToString()));
                        break;
                    case "fail":
                        client.SendMessage(e.ChatMessage.Channel, $"{e.ChatMessage.DisplayName} just requested a '{arg1}' failure");
                        OnRaiseCommandEvent(new CommandEventArgs(ChatCommands.Failure, arg1.ToString(), arg2.ToString()));
                        break;
                    case "transmit":
                    case "simconnect_transmitclientevent":
                        client.SendMessage(e.ChatMessage.Channel, $"{e.ChatMessage.DisplayName} just requested to send the '{arg1}' client event");
                        try
                        {
                            OnRaiseCommandEvent(new CommandEventArgs(ChatCommands.TransmitClientEvent, arg1.ToString(), arg2.ToString()));
                        }
                        catch (KeyNotFoundException ex)
                        {
                            client.SendMessage(e.ChatMessage.Channel, $"{e.ChatMessage.DisplayName}, the client rejected your request due to the following error: {ex.Message}");
                        }
                        break;
                    default:
                        client.SendMessage(e.ChatMessage.Channel, $"{e.ChatMessage.DisplayName} just requested unknown command '{cmd}'");
                        break;
                }
            }
        }

        private void OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            client.SendMessage(e.Channel, $"Thanks for subscribing, {e.Subscriber.DisplayName}!");
        }

        private void OnConnected(object sender, OnConnectedArgs e)
        {
            log.Info($"[TwitchClient] Connected to {e.AutoJoinChannel}");
        }

        private void OnLog(object sender, OnLogArgs e)
        {
            log.Info($"[TwitchClient] {e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }
    }
}
