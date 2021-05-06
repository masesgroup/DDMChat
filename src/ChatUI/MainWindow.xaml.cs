﻿/*
* MIT License
* 
* Copyright(c) 2021 S4I s.r.l. (a MASES Group company)
* www.s4i.it www.masesgroup.com
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using MASES.S4I.CommunicationLib;
using MASES.DataDistributionManager.Bindings;
using MASES.DataDistributionManager.Bindings.Configuration;
using MASES.S4I.ChatLib;

namespace MASES.S4I.ChatUI
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //internal used by the configuration windows
        internal ChatUser UserProfile = null;
        internal Guid chatID = Guid.Empty;

        #region private
        ConfigurationWindow configurationWin;
        CommunicationModule messageModule = new CommunicationModule();
        CommunicationModule userModule = new CommunicationModule();
        CommunicationModule[] comModules;
        bool firstActivation = true;
        #endregion private

        #region public
        public AddressBook book = new AddressBook();
        #endregion public

        #region DependencyProperty
        public String CommunicationState
        {
            get { return (String)GetValue(CommunicationStateProperty); }
            set { SetValue(CommunicationStateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CommunicationState.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommunicationStateProperty =
            DependencyProperty.Register("CommunicationState", typeof(String), typeof(MainWindow), new PropertyMetadata(string.Empty));


        public String CommunicationStateDescription
        {
            get { return (String)GetValue(CommunicationStateDescriptionProperty); }
            set { SetValue(CommunicationStateDescriptionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CommunicationState.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommunicationStateDescriptionProperty =
            DependencyProperty.Register("CommunicationStateDescription", typeof(String), typeof(MainWindow), new PropertyMetadata(string.Empty));

        public string TextArea
        {
            get { return (string)GetValue(TextAreaProperty); }
            set { SetValue(TextAreaProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TextArea.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextAreaProperty =
            DependencyProperty.Register("TextArea", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

        public bool StartServer
        {
            get { return (bool)GetValue(StartServerProperty); }
            set { SetValue(StartServerProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StartServer.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StartServerProperty =
            DependencyProperty.Register("StartServer", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        #endregion DependencyProperty

        #region communicationLib
        /// <summary>
        /// Connect to the communication server and create channels
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Connect(object sender, RoutedEventArgs e)
        {
            messageModule.ChannelName = "messages";
            messageModule.LogFileName = Constants.messageLog;
            userModule.ChannelName = "users";
            userModule.LogFileName = Constants.userLog;
            foreach (var comModule in comModules)
            {
                comModule.Id = chatID;
            }
            foreach (var comModule in comModules)
            {
                comModule.StartLocalServer = StartServer;

                if (comModule.StartLocalServer)
                {
                    if (!firstActivation) comModule.StartLocalServer = false;
                    else firstActivation = false;
                }

                if (ComboType.SelectedItem.ToString() == "Kafka")
                    comModule.TransportType = TransportEnum.KAFKA;
                else
                    comModule.TransportType = TransportEnum.OPEN_DDS;
                comModule.Configuration = Configuration(comModule);
                comModule.Activate();
            }
        }

        /// <summary>
        /// Manage incoming messages 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cm_MessageEvent(object sender, MsgEventArgs e)
        {
            if (e.DataType == typeof(string))
            {
                bool received = true;
                Message decoded = Message.FromJson(e.Data as string);
                if (decoded.Sender == (sender as CommunicationModule).Id) received = false;
                // we received a non decryptable message
                if (decoded.Kind == MessageKindType.VOID) return;
                string displayName;
                this.Dispatcher.Invoke(() =>
                {
                    if (sender == userModule)
                    {
                        if (decoded.Kind == MessageKindType.USER)
                        {
                            if (book.Add(decoded.Sender, (decoded as ChatUser)) &&
                               (decoded as ChatUser).Sender != UserProfile.Sender)
                            {
                                HelloMessage(decoded as ChatUser);
                            }
                        }
                    }
                    else
                    {
                        ChatUser cu = book.RetrieveUser(decoded.Sender);
                        displayName = (cu != null) ? cu.Name + " " + cu.LastName : decoded.Sender.GetHashCode().ToString();
                        string verified = (decoded.Verified) ? "V+" : "!?";

                        if (received)
                            TextArea += string.Format("{0} {1}-{2}>> {3}{4}", verified, e.Timestamp.ToShortTimeString(), displayName, decoded.StringContent, Environment.NewLine);
                        else
                            TextArea += string.Format("{0} {1}-{2}-- {3}{4}", verified, e.Timestamp.ToShortTimeString(), displayName, decoded.StringContent, Environment.NewLine);
                    }
                });
            }
        }

        /// <summary>
        /// Disconnect from the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Disconnect(object sender, RoutedEventArgs e)
        {
            foreach (var comModule in comModules)
            {
                comModule.Deactivate();
            }
        }

        /// <summary>
        /// Manage the status changes and in case of connection to the user channels send the Profile
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cm_StatusChanged(object sender, StatusEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                CommunicationState = e.NewState.Status.ToString();
                CommunicationStateDescription = e.NewState.Description;
            });
            if (sender == userModule && e.NewState.Status == StatusEnum.CONNECTED)
            {
                HelloMessage();
            }
        }
        #endregion communicationLib

        #region window
        /// <summary>
        /// Open the setting window 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnActivated(EventArgs e)
        {
            configurationWin = new ConfigurationWindow();
            //also in case of newly created user read it and use as default
            UserProfile = configurationWin.Profile;
            chatID = UserProfile.Sender;
            // If UserProfile is the default user force a setup
            if (configurationWin.NewlyCreatedUser)
            {
                //open the setting windows
                configurationWin.Owner = this;
                configurationWin.ShowDialog();
            }
            base.OnActivated(e);
        }

        public MainWindow()
        {
            book.Load();
            InitializeComponent();
            comModules = new CommunicationModule[] { userModule, messageModule };
            foreach (var comModule in comModules)
            {
                comModule.MessageEvent += Cm_MessageEvent;
                comModule.StatusChanged += Cm_StatusChanged;
            }
            DataContext = this;
            ComboType.ItemsSource = new string[] { "Kafka", "OpenDDS" };
            ComboType.SelectedIndex = 0;
            Contacts.ItemsSource = book.UserList;
        }
        #endregion window

        #region UIevents
        /// <summary>
        /// Send message to the channel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Send(object sender, RoutedEventArgs e)
        {
            List<ChatUser> selectedToSend = new List<ChatUser>();
            foreach (ChatUser cu in book.UserList)
            {
                if (cu.Selected) selectedToSend.Add(cu);
            }

            this.Dispatcher.Invoke(() =>
            {
                Message textMessage = new Message()
                {
                    Sender = messageModule.Id,
                    Kind = MessageKindType.STRING,
                    StringContent = MessageText.Text
                };
                if (selectedToSend.Count == 0)
                {
                    SignedMessage signedTextMessage = new SignedMessage(textMessage.ToJson());
                    messageModule.SendMessage<string>(signedTextMessage.ToJson());
                }
                else
                {
                    bool sentToMyself = false;
                    foreach (ChatUser cu in selectedToSend)
                    {
                        textMessage.Destination = cu.Sender;
                        SignedMessage signedTextMessage = new SignedMessage(textMessage.ToJson());
                        messageModule.SendMessage<string>(signedTextMessage.ToJson());
                        if (cu.Sender == UserProfile.Sender) sentToMyself = true;
                    }
                    if (!sentToMyself)
                    {
                        //send always a copy of the encrypted message to myself
                        textMessage.Destination = UserProfile.Sender;
                        SignedMessage signedTextMessage = new SignedMessage(textMessage.ToJson());
                        messageModule.SendMessage<string>(signedTextMessage.ToJson());
                    }
                }
                MessageText.Text = string.Empty;
            });
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (ChatUser cu in book.UserList)
            {
                cu.Selected = true;
            }
        }

        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (ChatUser cu in book.UserList)
            {
                cu.Selected = false;
            }
        }

        private void MessageText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return) Send(this, e);
        }

        /// <summary>
        /// Open the configuration window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Configure_button_click(object sender, RoutedEventArgs e)
        {
            configurationWin = new ConfigurationWindow();
            configurationWin.Owner = this;
            configurationWin.ShowDialog();
        }
        #endregion UIevents

        #region privateMethods
        private void HelloMessage(ChatUser cu = null)
        {
            if (cu != null)
            {
                UserProfile.Destination = cu.Sender;
            }
            SignedMessage signedUserProfileMessage = new SignedMessage(UserProfile.ToJson());
            userModule.SendMessage<string>(signedUserProfileMessage.ToJson(), messageModule.Id.ToString());
        }

        /// <summary>
        /// Get the right configuration for the module
        /// Should be managed in setting window
        /// </summary>
        /// <param name="cm"></param>
        /// <returns></returns>
        private CommonConfiguration Configuration(CommunicationModule cm)
        {
            CommonConfiguration conf = null;
            switch (cm.TransportType)
            {
                case TransportEnum.OPEN_DDS:
                    OpenDDSConfiguration oddsConfiguration = new OpenDDSConfiguration()
                    {
                        OpenDDSArgs = new OpenDDSConfiguration.OpenDDSArgsConfiguration()
                        {
                            DCPSConfigFile = "dds_tcp_conf.ini",
                            DCPSTransportDebugLevel = 10,
                        },
                        DCPSInfoRepo = new OpenDDSConfiguration.DCPSInfoRepoConfiguration()
                        {
                            Autostart = StartServer,
                            ORBEndpoint = "iiop://localhost:12345",
                        }
                    };
                    OpenDDSChannelConfiguration oddsChannelConf = new OpenDDSChannelConfiguration(oddsConfiguration)
                    {
                        TopicQos = new TopicQosConfiguration()
                        {
                            TopicDataQosPolicy = new TopicDataQosPolicyConfiguration()
                            {
                                Value = new byte[] { 100, 23 }
                            },
                            DurabilityQosPolicy = new DurabilityQosPolicyConfiguration()
                            {
                                Kind = DurabilityQosPolicyConfiguration.DurabilityQosPolicyKind.PERSISTENT_DURABILITY_QOS
                            },
                            DurabilityServiceQosPolicy = new DurabilityServiceQosPolicyConfiguration()
                            {
                                HistoryDepth = 100,
                                Kind = DurabilityServiceQosPolicyConfiguration.HistoryQosPolicyKind.KEEP_ALL_HISTORY_QOS,
                            },
                            ReliabilityQosPolicy = new ReliabilityQosPolicyConfiguration()
                            {
                                Kind = ReliabilityQosPolicyConfiguration.ReliabilityQosPolicyKind.RELIABLE_RELIABILITY_QOS
                            }
                        },
                        SubscriberQos = new SubscriberQosConfiguration()
                        {
                            EntityFactoryQosPolicy = new EntityFactoryQosPolicyConfiguration()
                            {
                                AutoenableCreatedEntities = true
                            },
                        }
                    };
                    conf = oddsChannelConf;
                    break;
                case TransportEnum.KAFKA:
                    KafkaConfiguration kConfiguration = new KafkaConfiguration()
                    {

                    };
                    KafkaChannelConfiguration kChannelConfiguration = new KafkaChannelConfiguration(kConfiguration)
                    {
                        AutoOffsetReset = (cm.ChannelName == "users") ? AutoOffsetResetType.beginning : AutoOffsetResetType.latest,
                        InitialOffset = (cm.ChannelName == "users") ? InitialOffsetTypes.Beginning : InitialOffsetTypes.Stored,
                        ClientId = "chat" + cm.ChannelName + cm.Id.ToString(),
                        GroupId = "chatGrp" + cm.Id.ToString(),
                        BootstrapBrokers = "206.189.214.143:9093",
                    };
                    conf = kChannelConfiguration;
                    break;
            }
            return conf;
        }
        #endregion privateMethods
    }
}
