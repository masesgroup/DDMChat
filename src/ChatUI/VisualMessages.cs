using MASES.S4I.ChatLib;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MASES.S4I.ChatUI
{
    /// <summary>
    /// Class to manage messages that shall be displayed
    /// </summary>
    public class VisualMessage
    {
        internal Message Message { get; set; }
        internal ChatUser User { get; set; }

        /// <summary>
        /// Alignement of the message in the chat
        /// </summary>
        public HorizontalAlignment Alignment { get; set; }


        /// <summary>
        /// Index of the message in the array
        /// </summary>
        public int Idx
        {
            get;
            internal set;
        }

        /// <summary>
        /// Return the StringContent of the message
        /// </summary>
        public string StringContent
        {
            get
            {
                return Message.StringContent;
            }
        }

        /// <summary>
        /// Name of the sender or sender id 
        /// </summary>
        public string SenderName
        {
            get
            {
                if (User != null)
                    return string.Format("{0} {1}", User.Name, User.LastName);
                else
                    return Message.Sender.ToString();
            }
        }

        /// <summary>
        /// return true if the message contains a file
        /// </summary>
        public bool HaveDownload
        {
            get
            {
                if (Message.Kind == MessageKindType.FILE ||
                    Message.Kind == MessageKindType.IMAGE)
                    return true;
                return false;
            }
        }

        /// <summary>
        /// True if the message is encrypted
        /// </summary>
        public bool Encrypted
        {
            get
            {
                return Message.Destination != Guid.Empty;
            }
        }

        /// <summary>
        /// encapsulate Message veerified property
        /// </summary>
        public bool Verified
        {
            get
            {
                return Message.Verified;
            }
        }

        /// <summary>
        /// The file contained in the message 
        /// </summary>
        public ChatFileContent File
        {
            get
            {
                try
                {
                    switch (Message.Kind)
                    {
                        case MessageKindType.IMAGE:
                            ChatImage ci = Message as ChatImage;
                            return ci.ImageContent.RawFile;
                        case MessageKindType.FILE:
                            ChatFile cf = Message as ChatFile;
                            return cf.FileContent;
                    }
                }
                catch (Exception ex)
                {
                    //TODO: add logging

                }
                return null;
            }
        }

        /// <summary>
        /// The name of the file contained in the message
        /// </summary>
        public string FileName
        {
            get
            {
                try
                {
                    switch (Message.Kind)
                    {
                        case MessageKindType.IMAGE:
                            ChatImage ci = Message as ChatImage;
                            return ci.Name;
                        case MessageKindType.FILE:
                            ChatFile cf = Message as ChatFile;
                            return cf.Name;
                    }
                }
                catch (Exception ex)
                {
                    //TODO: add logging

                }
                return null;
            }
        }
    }

    /// <summary>
    /// A class to manage the received messages
    /// implements <see cref="INotifyPropertyChanged"/> to be used in user interface
    /// </summary>
    public class ReceivedMessages : INotifyPropertyChanged
    {
        public ObservableCollection<VisualMessage> MessageList = new ObservableCollection<VisualMessage>();

        /// <summary>
        /// Add a message to the exposed MessageList
        /// </summary>
        /// <param name="receivedMessage">the <see cref="Message"/> message to add</param>
        public void Add(Message receivedMessage, ChatUser cu, bool received)
        {
            HorizontalAlignment alignment = (received) ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            MessageList.Add(new VisualMessage() { Message = receivedMessage, User = cu, Idx = MessageList.Count, Alignment = alignment });
            NotifyPropertyChanged("MessageList");
        }

        /// <summary>
        /// The PropertyChangedEventHandler event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.  
        // The CallerMemberName attribute that is applied to the optional propertyName  
        // parameter causes the property name of the caller to be substituted as an argument.  
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
