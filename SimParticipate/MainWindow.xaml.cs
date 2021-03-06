﻿using System;
using System.Collections.Generic;
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

namespace SimParticipate
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Log log;
        TwitchBot bot;
        SimConnectInstance sc;
        Config cfg;

        public MainWindow()
        {
            InitializeComponent();

            //new SimEventManager(new BeatlesBlog.SimConnect.SimConnect());

            log = Log.Instance;

            cfg = new Config("twitch_auth.xml");

            sc = new SimConnectInstance();
            sc.OpenEvent += OnSimConnectOpen;
            sc.Connect();

            bot = new TwitchBot(cfg.Get<string>("username"), cfg.Get<string>("token"));
            bot.CommandEvent += OnBotCommand;
        }

        private void OnSimConnectOpen(object sender, OpenEventArgs e)
        {
            log.Info("SimConnect open event in MainWindow");
        }

        private void OnBotCommand(object sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case ChatCommands.Echo:
                    break;
                case ChatCommands.Failure:
                    OnFailureCommand(e);
                    break;
                case ChatCommands.Text:
                    sc.Text(10.0f, $"Message from {e.PrimaryArgument}: \"{e.SecondaryArgument}\"");
                    break;
                case ChatCommands.TransmitClientEvent:
                    OnClientEventCommand(e);
                    break;
                default:
                    log.Warning($"TwitchBot raised unknown command {Enum.GetName(typeof(ChatCommands), e.Command)}");
                    break;
            }
        }

        private void OnClientEventCommand(CommandEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.SecondaryArgument))
            {
                log.Info($"Attempting to TransmitClientEvent `{e.PrimaryArgument}`");
                sc.TransmitClientEvent(e.PrimaryArgument);
            }
            else
            {
                var result = uint.TryParse(e.SecondaryArgument, out uint data);
                if (result)
                {
                    log.Info($"Attempting to TransmitClientEvent `{e.PrimaryArgument}` with data {data}");
                    sc.TransmitClientEvent(e.PrimaryArgument, data);
                }
                else
                {
                    log.Info($"Unable to parse secondary argument `{e.SecondaryArgument}` as uint");
                }
            }
        }

        private void OnFailureCommand(CommandEventArgs e)
        {
            switch (e.PrimaryArgument.ToUpperInvariant())
            {
                case "VACUUM":
                    sc.TransmitClientEvent("TOGGLE_VACUUM_FAILURE");
                    break;
                case "ELECTRICAL":
                    sc.TransmitClientEvent("TOGGLE_ELECTRICAL_FAILURE");
                    break;
                case "PITOT":
                    sc.TransmitClientEvent("TOGGLE_PITOT_BLOCKAGE");
                    break;
                case "STATIC":
                    sc.TransmitClientEvent("TOGGLE_STATIC_PORT_BLOCKAGE");
                    break;
                case "HYDRAULIC":
                    sc.TransmitClientEvent("TOGGLE_HYDRAULIC_FAILURE");
                    break;
                case "BRAKE":
                    if (!string.IsNullOrWhiteSpace(e.SecondaryArgument))
                    {
                        switch (e.SecondaryArgument.ToUpperInvariant())
                        {
                            case "LEFT":
                                sc.TransmitClientEvent("TOGGLE_LEFT_BRAKE_FAILURE");
                                break;
                            case "RIGHT":
                                sc.TransmitClientEvent("TOGGLE_RIGHT_BRAKE_FAILURE");
                                break;
                            default:
                                log.Warning($"unknown brake failure argument {e.SecondaryArgument}");
                                break;
                        }
                    }
                    else
                    {
                        sc.TransmitClientEvent("TOGGLE_TOTAL_BRAKE_FAILURE");
                    }                    
                    break;
                case "ENGINE":
                    sc.TransmitClientEvent("TOGGLE_ENGINE1_FAILURE");
                    break;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Log.Instance.ConditionalSave();
        }
    }
}
