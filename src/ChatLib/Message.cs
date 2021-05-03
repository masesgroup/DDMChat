/*
* MIT License
* 
* Copyright(c) 2021  S4I s.r.l. (a MASES Group company)
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
using System.Text;
using Newtonsoft.Json;
using System.IO.Compression;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace MASES.S4I.ChatLib
{
    /// <summary>
    /// Defines the supported compression kinds
    /// </summary>
    public enum CompressionKindType : int
    {
        UNCOMPRESSED,
        GZIP,
    }

    /// <summary>
    /// Defines the supported images kinds
    /// </summary>
    public enum ImageKindType : int
    {
        JPG,
        PNG,
        GIF,
    }

    /// <summary>
    /// Defines the supported messages kinds
    /// </summary>
    public enum MessageKindType : int
    {
        STRING,
        IMAGE,
        URL,
        FILE,
        USER,
        ENCRYPTED,
        VOID,
    }


    /// <summary>
    /// A class to manage the contact list
    /// implements <see cref="INotifyPropertyChanged"/> to be used in user interface
    /// </summary>
    public class AddressBook : INotifyPropertyChanged
    {
        static Dictionary<Guid, ChatUser> book = new Dictionary<Guid, ChatUser>();
        public ObservableCollection<ChatUser> UserList = new ObservableCollection<ChatUser>();

        /// <summary>
        /// Retrieve a <see cref="ChatUser"/> from the address book
        /// </summary>
        /// <param name="id">the <see cref="Guid"/> of the contact</param>
        /// <returns>The <see cref="ChatUser"/> instance or null if not found</returns>
        public ChatUser RetrieveUser(Guid id)
        {
            if (book.ContainsKey(id)) return book[id];
            return null;
        }

        /// <summary>
        /// Add a contact to the exposed UserList
        /// </summary>
        /// <param name="id">the <see cref="Guid"/> of the contact</param>
        /// <param name="user">The <see cref="ChatUser"/> to add</param>
        /// <returns>Return True if the user is added, False elsewere</returns>
        public bool Add(Guid id, ChatUser user)
        {
            bool res = false;
            if (book.ContainsKey(id))
            {
                book[id] = user;
            }
            else
            {
                book.Add(id, user);
                res = true;
            }
            UserList.Clear();
            foreach (var cu in book.Values)
            {
                UserList.Add(cu);
            }
            NotifyPropertyChanged("UserList");
            Save();
            return res;
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

        /// <summary>
        /// Save the contact list to a file
        /// </summary>
        public void Save()
        {
            string serialized = JsonConvert.SerializeObject(book);
            File.WriteAllText(Constants.contactsFile, serialized);
        }

        /// <summary>
        /// Load the contact list from file and update the UserList property
        /// </summary>
        public void Load()
        {
            if (File.Exists(Constants.contactsFile))
            {
                string serialized = File.ReadAllText(Constants.contactsFile);
                book = JsonConvert.DeserializeObject<Dictionary<Guid, ChatUser>>(serialized);
                foreach (KeyValuePair<Guid, ChatUser> user in book)
                {
                    UserList.Clear();
                    foreach (var cu in book.Values)
                    {
                        UserList.Add(cu);
                    }
                }
            }
        }
    }


    /// <summary>
    /// This is the envelop for message signature
    /// </summary>
    public class SignedMessage
    {
        public byte[] Signature;
        public string JsonMessage;

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="jsonMessage">The json serialized message to send</param>
        public SignedMessage(string jsonMessage)
        {
            JsonMessage = jsonMessage;
            Signature = new SecureMe().Sign(JsonMessage);
        }

        /// <summary>
        /// Serializer
        /// </summary>
        /// <returns>the Json serialized <see cref="SignedMessage"/></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Deserializes
        /// </summary>
        /// <param name="str">the serialized <see cref="SignedMessage"/></param>
        /// <returns>The deserialized <see cref="SignedMessage"/> or null if deserialization is not possible</returns>
        public static SignedMessage FromJson(string str)
        {
            try
            {
                var res = JsonConvert.DeserializeObject<SignedMessage>(str);
                return res;
            }
            catch
            {
                // unable to decode to a SignedMessage
                return null;
            }
        }

        /// <summary>
        /// Verify the signature of a <see cref="SignedMessage"/>
        /// </summary>
        /// <param name="sm">The <see cref="SignedMessage"/> to verify</param>
        /// <param name="publicKey">The public key of the signer</param>
        /// <returns>True if the signature is verified, false elseware</returns>
        public static bool Verify(SignedMessage sm, string publicKey)
        {
            return new SecureMe().Verify(sm.JsonMessage, sm.Signature, publicKey);
        }
    }

    /// <summary>
    /// The message base class
    /// most of the methods are public to be serializable
    /// </summary>
    public class Message
    {
        AddressBook book = new AddressBook();
        /// <summary>
        /// The sender of the message
        /// </summary>
        public Guid Sender;
        /// <summary>
        /// The Destination of the message
        /// if the destination is valorized the message is sent using encryption elseware
        /// is broadcasted in plaitext to the chat
        /// </summary>
        public Guid Destination;
        /// <summary>
        /// TimeStamp of creation of the message
        /// </summary>
        public DateTime TimeStamp;
        /// <summary>
        /// Kind of the contained message
        /// used in serialization and deserialization
        /// </summary>
        public MessageKindType Kind;
        /// <summary>
        /// For basic messages the text of the message
        /// </summary>
        public string StringContent;
        /// <summary>
        /// If the message is encrypted DataContent contains the encrypted data
        /// </summary>
        public EncryptedMessage DataContent;
        /// <summary>
        /// True if the message signature was verified
        /// </summary>
        public bool Verified;

        /// <summary>
        /// Constructor
        /// </summary>
        public Message()
        {
            TimeStamp = DateTime.Now;
        }

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="destination">Prepare a message for the passed destination</param>
        public Message(Guid destination)
        {
            Destination = destination;
            TimeStamp = DateTime.Now;
        }

        /// <summary>
        /// Message serializer
        /// </summary>
        /// <returns>The Json serialized message</returns>
        public string ToJson()
        {
            if (Destination != Guid.Empty && Kind != MessageKindType.ENCRYPTED)
            {
                ChatUser destination = book.RetrieveUser(Destination);
                if (destination != null)
                {
                    Message containter = new Message();
                    containter.Sender = this.Sender;
                    containter.Kind = MessageKindType.ENCRYPTED;
                    containter.Destination = Destination;
                    ChatUser cu = destination;
                    //This is the recurion stop condiction and avoid stack overflow
                    this.Destination = Guid.Empty;
                    string pk = cu.PublicKey;
                    string json = this.ToJson();
                    byte[] encoded = Encoding.ASCII.GetBytes(json);
                    containter.DataContent = new SecureMe().EncryptMessage(pk, encoded);
                    return containter.ToJson();
                }
                else
                {
                    throw new Exception("User not in address book");
                }
            }
            else
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        /// <summary>
        /// Deserializer
        /// </summary>
        /// <param name="str">The Json serialized message</param>
        /// <returns>The deserialized <see cref="Message"/></returns>
        public static Message FromJson(string str)
        {
            SignedMessage sm = SignedMessage.FromJson(str);
            // if the message is a signed message decode the contained json
            if (sm != null)
            {
                str = sm.JsonMessage;
                Message deserialized = FromJson(str);
                ChatUser from = new AddressBook().RetrieveUser(deserialized.Sender);
                deserialized.Verified = (from != null && SignedMessage.Verify(sm, from.PublicKey));
                return deserialized;
            }

            var res = JsonConvert.DeserializeObject<Message>(str);
            res.Verified = false;
            switch (res.Kind)
            {
                case MessageKindType.STRING:
                case MessageKindType.URL:
                    {
                        return res;
                    }
                case MessageKindType.FILE:
                    {
                        var res2 = JsonConvert.DeserializeObject<ChatFile>(str);
                        return res2;
                    }
                case MessageKindType.IMAGE:
                    {
                        var res2 = JsonConvert.DeserializeObject<ChatImage>(str);
                        return res2;
                    }
                case MessageKindType.USER:
                    {
                        var res2 = JsonConvert.DeserializeObject<ChatUser>(str);
                        return res2;
                    }
                case MessageKindType.ENCRYPTED:
                    {
                        byte[] decrypted = new SecureMe().DecryptMessage(res.DataContent);
                        if (decrypted == null) return new Message() { Kind = MessageKindType.VOID };
                        var res2 = FromJson(Encoding.ASCII.GetString(new SecureMe().DecryptMessage(res.DataContent)));
                        //restore destination from envelop 
                        res2.Destination = res.Destination;
                        //restore verified from envelop
                        res2.Verified = res.Verified;
                        return res2;
                    }
                default: throw new InvalidOperationException(string.Format("Unrecognized message type {0}", res.Kind));
            }
        }
    }

    /// <summary>
    /// This class derived from <see cref="Message"/> contains user contact information
    /// implements <see cref="INotifyPropertyChanged"/> to be used directly inside user interface
    /// </summary>
    public class ChatUser : Message, INotifyPropertyChanged
    {
        /// <summary>
        /// The PropertyChangedEventHandler event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Name of the contact
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Last name of the contact
        /// </summary>
        public string LastName { get; set; }

        bool selected;
        /// <summary>
        /// Property useful for contact selection in a collection 
        /// </summary>
        public bool Selected
        {
            get
            {
                return selected;
            }

            set
            {
                selected = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// The profile picture 
        /// </summary>
        public ChatImageContent ProfilePicture;
        /// <summary>
        /// The PublicKey of the contact
        /// </summary>
        public string PublicKey;

        /// <summary>
        /// Constructor
        /// </summary>
        public ChatUser()
        {
            PublicKey = new SecureMe().PublicKey;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Class for messages containing images
    /// </summary>
    public class ChatImage : Message
    {
        /// <summary>
        /// Name of the image
        /// </summary>
        public string Name;
        /// <summary>
        /// Description
        /// </summary>
        public string Description;
        /// <summary>
        /// Image content as <see cref="ChatImageContent"/>
        /// </summary>
        public ChatImageContent ImageContent;
    }

    /// <summary>
    /// Class containing the raw file and the format of an image
    /// </summary>
    public class ChatImageContent
    {
        /// <summary>
        /// A <see cref="ChatFileContent"/> containing the image file row data
        /// </summary>
        public ChatFileContent RawFile;

        /// <summary>
        /// The <see cref="ImageKindType"/> of the image
        /// </summary>
        public ImageKindType Format;
    }


    /// <summary>
    /// A class for a generic file to be transmitted in chat
    /// </summary>
    public class ChatFile : Message
    {
        /// <summary>
        /// Name of the file
        /// </summary>
        public string Name;
        /// <summary>
        /// Description
        /// </summary>
        public string Description;
        /// <summary>
        /// A <see cref="ChatFileContent"/> containing the file row data
        /// </summary>
        public ChatFileContent FileContent;
    }

    /// <summary>
    ///  A class containing the file row data and methods for compression/decompression
    /// </summary>
    public class ChatFileContent
    {
        //TODO: Manage
        /// <summary>
        /// The file Raw data 
        /// </summary>
        public byte[] Raw;
        /// <summary>
        /// Property to set/get the Raw data, automatically use the compression
        /// </summary>
        public byte[] Content
        {
            get
            {
                switch (Compression)
                {
                    case CompressionKindType.GZIP:
                        return Decompress(Raw);
                    case CompressionKindType.UNCOMPRESSED:
                        return Raw;
                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                switch (Compression)
                {
                    case CompressionKindType.GZIP:
                        Raw = Compress(value);
                        break;
                    case CompressionKindType.UNCOMPRESSED:
                        Raw = value;
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Compress
        /// </summary>
        /// <param name="data">The uncompressed data to compress</param>
        /// <returns>The compressed data</returns>
        public static byte[] Compress(byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream dstream = new GZipStream(output, CompressionLevel.Optimal))
                {
                    dstream.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }

        /// <summary>
        /// Decompress
        /// </summary>
        /// <param name="data">Compressed data to decompress</param>
        /// <returns>The deompressed data</returns>
        public static byte[] Decompress(byte[] data)
        {
            using (MemoryStream input = new MemoryStream(data))
            {
                using (MemoryStream output = new MemoryStream())
                {
                    using (GZipStream dstream = new GZipStream(input, CompressionMode.Decompress))
                    {
                        dstream.CopyTo(output);
                        return output.ToArray();
                    }
                }
            }
        }
        /// <summary>
        /// Compression kind
        /// </summary>
        public CompressionKindType Compression;
    }
}
