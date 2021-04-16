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
using System.Text;
using System.Threading.Tasks;
using MASES.DataDistributionManager.Bindings;
using MASES.DataDistributionManager.Bindings.Configuration;
using MASES.DataDistributionManager.Bindings.Interop;
using System.Threading;
using System.IO;

namespace MASES.S4I.CommunicationLib
{
    public class CommunicationModule : ICommunication
    {
        #region Private
        // Create the smartdatadistribution instance
        SmartDataDistribution dataDistribution = new SmartDataDistribution();
        SmartDataDistributionChannel Channel;
        // Initializa the status to UNDEFINED, this will be used to manage the firs start
        ExtendedStatus _Status = new ExtendedStatus { Status = StatusEnum.UNDEFINED, Description = "UNDEFINED" };
        CommonConfiguration _Configuration;
        //Synchronization object to serializa access to code 
        object CriticalSection = new object();
        #endregion

        #region Public 
        /// <summary>
        /// If true try to start a local server (only checked in OPEN_DDS)
        /// </summary>
        public bool StartLocalServer { get; set; }
        /// <summary>
        /// Set the ChannelName (this value represent the Topic id in Kafka e OpenDDS)
        /// </summary>
        public string ChannelName { get; set; }
        /// <summary>
        /// The Id that identify the application (passed ad key in Kafka and OpenDDS messages)
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// Define the Transport subsystem (KAFKA or OPEN_DDS)
        /// </summary>
        public TransportEnum TransportType { get; set; }
        /// <summary>
        /// If set the DataDistribution_LoggingEvent logs to the setted filename
        /// </summary>
        public String LogFileName { get; set; }
        //ICommunication event handlers
        public event EventHandler<MsgEventArgs> MessageEvent;
        public event EventHandler<StatusEventArgs> StatusChanged;
        #endregion



        /// <summary>
        /// The status of the communication subsistem, change on status rises the StatusChanged event
        /// </summary>
        public ExtendedStatus Status
        {
            get
            {
                return _Status;
            }

            private set
            {
                StatusEventArgs sea = new StatusEventArgs { NewState = value, OldState = _Status };
                _Status = value;
                // Make a temporary copy of the event to avoid possibility of
                // a race condition if the last subscriber unsubscribes
                // immediately after the null check and before the event is raised.
                EventHandler<StatusEventArgs> raiseEvent = StatusChanged;

                // Event will be null if there are no subscribers
                if (raiseEvent != null)
                {
                    //raise the event.
                    raiseEvent(this, sea);
                }
            }
        }

        /// <summary>
        /// Set Get the configuration. The set configuration shall be of the type declared in TransportType
        /// </summary>
        public CommonConfiguration Configuration
        {
            get
            {
                if (TransportType == TransportEnum.KAFKA
                    && !(_Configuration is KafkaConfiguration)) return null;
                if (TransportType == TransportEnum.OPEN_DDS
                    && !(_Configuration is OpenDDSConfiguration)) return null;
                return _Configuration;
            }

            set
            {
                if (TransportType == TransportEnum.KAFKA
                    && !(value is KafkaConfiguration))
                    throw new ArgumentException("The passed configuration is not a valid Kafka configuration");
                if (TransportType == TransportEnum.OPEN_DDS
                    && !(value is OpenDDSConfiguration))
                    throw new ArgumentException("The passed configuration is not a valid OpenDDS configuration");
                _Configuration = value;
            }
        }


        /// <summary>
        /// Initialize (only the first time) the communication, Start the subsystem and create the communiction channel
        /// The method is decoupled with a Thread, becouse of this the commmunication state shall be checked to avoid undesidered behaviours
        /// </summary>
        public void Activate()
        {
            Thread ActivationThread = new Thread(InternalActivate);
            ActivationThread.Start();
        }

        void InternalActivate()
        {
            lock (CriticalSection)
            {
                if (Status.Status == StatusEnum.CONNECTED
                    || Status.Status == StatusEnum.COMMUNICATING
                    || Status.Status == StatusEnum.COMMUNICATION_ERROR
                    || Status.Status == StatusEnum.INITIALIZED
                    || Status.Status == StatusEnum.COMMUNICATION_ERROR
                    ) return;
                OPERATION_RESULT Res = new OPERATION_RESULT();
                //dataDistribution = new SmartDataDistribution();

                dataDistribution.LoggingEvent += DataDistribution_LoggingEvent;
                try
                {

                    if (Status.Status == StatusEnum.UNDEFINED)
                        Res = dataDistribution.Initialize(Configuration);
                    Status = new ExtendedStatus { Status = StatusEnum.INITIALIZED, Description = "INITIALIZED" };
                    if (Res.Failed)
                    {
                        Status = new ExtendedStatus
                        {
                            Status = StatusEnum.ERROR,
                            Description = "Initialization error"
                        };
                    }
                    Res = dataDistribution.Start(uint.MaxValue);
                    if (Res.Failed)
                    {
                        Status = new ExtendedStatus
                        {
                            Status = StatusEnum.ERROR,
                            Description = "Start error"
                        };
                    }

                    Channel = dataDistribution.CreateSmartChannel<SmartDataDistributionChannel>(ChannelName, Configuration);
                    Channel.DataAvailable += Channel_DataAvailable;
                    Channel.ConditionOrError += Channel_ConditionOrError;
                    Res = Channel.StartChannel(uint.MaxValue);
                    if (Res.Succeeded)
                    {
                        Status = new ExtendedStatus
                        {
                            Status = StatusEnum.CONNECTED,
                            Description = "Channel connection OK"
                        };
                    }
                    if (Res.Failed)
                    {
                        Status = new ExtendedStatus
                        {
                            Status = StatusEnum.ERROR,
                            Description = "Channel Start error"
                        };
                    }
                    Channel.SeekChannel(0);
                }
                catch (Exception ex)
                {
                    Status = new ExtendedStatus
                    {
                        Status = StatusEnum.ERROR,
                        Description = ex.Message
                    };
                }
            }
        }

        /// <summary>
        /// Stop the communication channel and the communication subsystem
        /// The method is decoupled with a Thread, becouse of this the commmunication state shall be checked to avoid undesidered behaviours 
        /// </summary>
        public void Deactivate()
        {
            Thread DectivationThread = new Thread(InternalDeactivate);
            DectivationThread.Start();
        }

        void InternalDeactivate()
        {
            lock (CriticalSection)
            {
                if (Status.Status == StatusEnum.DEACTIVATED
                || Status.Status == StatusEnum.ERROR
                || Status.Status == StatusEnum.INITIALIZED
                || Status.Status == StatusEnum.UNDEFINED
                ) return;
                dataDistribution.LoggingEvent -= DataDistribution_LoggingEvent;
                Channel.DataAvailable -= Channel_DataAvailable;
                Channel.ConditionOrError -= Channel_ConditionOrError;
                OPERATION_RESULT Res = Channel.StopChannel(10000);
                if (Res.Succeeded)
                {
                    Status = new ExtendedStatus
                    {
                        Status = StatusEnum.INITIALIZED,
                        Description = "Channel Stopped"
                    };
                }
                else
                {
                    Status = new ExtendedStatus
                    {
                        Status = StatusEnum.DEACTIVATION_ERROR,
                        Description = "Error in channel deactivation"
                    };
                }
                Res = dataDistribution.Stop(10000);
                if (Res.Succeeded)
                {
                    Status = new ExtendedStatus
                    {
                        Status = StatusEnum.DEACTIVATED,
                        Description = "Communication module Stopped"
                    };
                }
                else
                {
                    Status = new ExtendedStatus
                    {
                        Status = StatusEnum.DEACTIVATION_ERROR,
                        Description = "Error in communication module deactivation"
                    };
                }
                //dataDistribution = null;
            }
        }

        /// <summary>
        /// Send a message on the communication channel
        /// The sending is decoupled with an Async task paradigm 
        /// </summary>
        /// <typeparam name="T">the message type, by now only strings are fully supported</typeparam>
        /// <param name="message">the message to send</param>
        public void SendMessage<T>(T message, string key = null)
        {
            Task asyncSendMessage = InternalSendMessage<T>(message,key);
        }

        async Task InternalSendMessage<T>(T message, string key = null)
        {
            await Task.Run(() =>
            {
                if (!(Status.Status == StatusEnum.CONNECTED
                || Status.Status == StatusEnum.COMMUNICATING
                || Status.Status == StatusEnum.COMMUNICATION_ERROR))
                    throw new Exception("Unable to send messages in status: " + Status.Status);
                if (Id == null) throw new Exception("Id not configured");
                if (!(message is string)) throw new NotImplementedException("Only string messages are implemented");
                byte[] msg = ASCIIEncoding.ASCII.GetBytes(message as string);
                string strMessage = message as string;
                OPERATION_RESULT res = Channel.WriteOnChannel(key, message as string);
                //OPERATION_RESULT res = Channel.WriteOnChannel(key, msg);
                if (res.Failed)
                {
                    Status = new ExtendedStatus
                    {
                        Status = StatusEnum.COMMUNICATION_ERROR,
                        Description = "Error sending message"
                    };
                }
                else
                {
                    Status = new ExtendedStatus
                    {
                        Status = StatusEnum.COMMUNICATING,
                        Description = "Ok"
                    };
                }
            });
        }

        private void Channel_ConditionOrError(object sender, ConditionOrErrorEventArgs e)
        {
            Status = new ExtendedStatus
            {
                Status = StatusEnum.COMMUNICATION_ERROR,
                Description = string.Format("Received event from {0} with ErrorCode {1} NativeCode {2} SubSystemReason {3}", e.ChannelName, e.ErrorCode, e.NativeCode, e.SubSystemReason)
            };
            if (!String.IsNullOrEmpty(LogFileName))
            {
                File.AppendAllText(LogFileName, String.Format("Timestamp: {0} Received event from: {1} ErrorCode: {2} NativeCode: {3} SubSystemReason: {4} {5}", DateTime.Now, e.ChannelName, e.ErrorCode, e.NativeCode, e.SubSystemReason,  Environment.NewLine));
            }
        }

        private void Channel_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            MsgEventArgs mea = new MsgEventArgs();
            mea.Timestamp = DateTime.Now;
            //TODO: REMOVE
            //Guid guid;
            //mea.SenderId = Guid.TryParse(e.Key,out guid)? guid: Guid.Empty;
            //mea.EventType = (mea.SenderId == Id) ? EventTypeEnum.SENT : EventTypeEnum.RECEIVED;
            if (!String.IsNullOrEmpty(e.DecodedString))
            {
                mea.DataType = typeof(String);
                mea.Data = e.DecodedString;
            }
            else
            {
                mea.DataType = typeof(byte[]);
                mea.Data = e.Buffer;
            }

            Status = new ExtendedStatus
            {
                Status = StatusEnum.COMMUNICATING,
                Description = "Ok"
            };
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            EventHandler<MsgEventArgs> raiseEvent = MessageEvent;

            // Event will be null if there are no subscribers
            if (raiseEvent != null)
            {
                //raise the event.
                raiseEvent(this, mea);
            }
        }

        private void DataDistribution_LoggingEvent(object sender, LoggingEventArgs e)
        {
            if (!String.IsNullOrEmpty(LogFileName))
            {
                File.AppendAllText(LogFileName, String.Format("Timestamp: {0} Source: {1} Function: {2} - {3} {4}", DateTime.Now, e.Source, e.Function, e.LogString, Environment.NewLine));
            }
        }
    }
}

