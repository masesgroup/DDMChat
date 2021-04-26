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
    public enum CompressionKindType : int
    {
        UNCOMPRESSED,
        GZIP,
    }

    public enum ImageKindType : int
    {
        JPG,
        PNG,
        GIF,
    }

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

    public class AddressBook : INotifyPropertyChanged
    {
        static Dictionary<Guid, ChatUser> book = new Dictionary<Guid, ChatUser>();
        public ObservableCollection<ChatUser> UserList = new ObservableCollection<ChatUser>();


        public ChatUser RetrieveUser(Guid id)
        {
            if (book.ContainsKey(id)) return book[id];
            return null;
        }

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
            return res;

        }

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.  
        // The CallerMemberName attribute that is applied to the optional propertyName  
        // parameter causes the property name of the caller to be substituted as an argument.  
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class SignedMessage
    {
        public byte[] Signature;
        public string JsonMessage;

        public SignedMessage(string jsonMessage)
        {
            JsonMessage = jsonMessage;
            Signature = new SecureMe().Sign(JsonMessage);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

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

        public static bool Verify(SignedMessage sm, string publicKey)
        {
            return new SecureMe().Verify(sm.JsonMessage, sm.Signature, publicKey);
        }
    }

    public class Message
    {
        AddressBook book = new AddressBook();
        public Guid Sender;
        public Guid Destination;
        public DateTime TimeStamp;
        public MessageKindType Kind;
        public string StringContent;
        public EncryptedMessage DataContent;
        public bool Verified;

        public Message()
        {
            TimeStamp = DateTime.Now;
        }

        public Message(Guid destination)
        {
            Destination = destination;
            TimeStamp = DateTime.Now;
        }

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

    public class ChatUser : Message, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; set; }
        public string LastName { get; set; }

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

        public ChatImageContent ProfilePicture;
        public string PublicKey;

        bool selected;

        public ChatUser()
        {
            PublicKey = new SecureMe().PublicKey;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ChatImage : Message
    {
        public string Name;
        public string Description;
        public ChatImageContent ImageContent;
    }

    public class ChatImageContent
    {
        public ChatFileContent RawFile;
        public ImageKindType Format;
    }


    public class ChatFile : Message
    {
        public string Name;
        public string Description;
        public ChatFileContent FileContent;
    }

    public class ChatFileContent
    {
        //TODO: Manage
        public byte[] Raw;
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
        public CompressionKindType Compression;
    }
}
