/*
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
using System.Windows;
using MASES.DataDistributionManager.Bindings.Configuration;
using MASES.S4I.ChatLib;
using System.IO;

namespace MASES.S4I.ChatUI
{
    /// <summary>
    /// Logica di interazione per Configuration.xaml
    /// </summary>
    public partial class ConfigurationWindow : Window
    {
        public ChatUser Profile { get; set; }
        public bool NewlyCreatedUser = true;
        public OpenDDSConfiguration ODDSConfiguration { get; set; }
        public KafkaConfiguration KConfiguration { get; set; }
        public ConfigurationWindow()
        {
            LoadProfile();
            InitializeComponent();
            DataContext = this;
        }

        void LoadProfile()
        {
            if (File.Exists(Constants.userProfile))
            {
                Profile = Message.FromJson(File.ReadAllText(Constants.userProfile)) as ChatUser;
                NewlyCreatedUser = false;
            }
            else
            {
                Guid chatID = Guid.NewGuid();
                //BitmapImage img = new BitmapImage(new Uri(constants.profilePhoto));
                //JpegBitmapEncoder jbe = new JpegBitmapEncoder();
                //JpegBitmapEncoder.
                Profile = new ChatUser()
                {
                    Sender = chatID,
                    Kind = MessageKindType.USER,
                    Name = "Name",
                    LastName = "LastName",
                    ProfilePicture = new ChatImageContent()
                    {
                        Format = ImageKindType.JPG,
                        RawFile = new ChatFileContent()
                        {
                            Content = File.ReadAllBytes(Constants.profilePhoto),
                            Compression = CompressionKindType.UNCOMPRESSED,
                        }
                    },
                    StringContent = "Hello everybody I present myself!!",
                };
                File.WriteAllText(Constants.userProfile, Profile.ToJson());
            }
        }

        private void Save_button(object sender, RoutedEventArgs e)
        {
            File.WriteAllText(Constants.userProfile, Profile.ToJson());
            (this.Owner as MainWindow).chatID = Profile.Sender;
            (this.Owner as MainWindow).UserProfile = Profile;
            this.DialogResult = true;
        }
    }
}
