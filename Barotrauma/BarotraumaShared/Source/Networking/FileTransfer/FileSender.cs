﻿using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Barotrauma.Networking
{
    enum FileTransferStatus
    {
        NotStarted, Sending, Receiving, Finished, Canceled, Error
    }

    enum FileTransferMessageType
    {
        Unknown, Initiate, Data, Cancel
    }

    enum FileTransferType
    {
        Submarine, CampaignSave
    }

    class FileSender
    {
        public class FileTransferOut
        {
            private byte[] data;

            private DateTime startingTime;

            private NetConnection connection;

            public FileTransferStatus Status;
            
            public string FileName
            {
                get;
                private set;
            }

            public string FilePath
            {
                get;
                private set;
            }

            public FileTransferType FileType
            {
                get;
                private set;
            }

            public float Progress
            {
                get { return SentOffset / (float)Data.Length; }
            }

            public float WaitTimer
            {
                get;
                set;
            }

            public byte[] Data
            {
                get { return data; }
            }

            public int SentOffset
            {
                get;
                set;
            }

            public NetConnection Connection
            {
                get { return connection; }
            }

            public int SequenceChannel;

            public FileTransferOut(NetConnection recipient, FileTransferType fileType, string filePath)
            {
                connection = recipient;

                FileType = fileType;
                FilePath = filePath;
                FileName = Path.GetFileName(filePath);

                Status = FileTransferStatus.NotStarted;
                
                startingTime = DateTime.Now;

                data = File.ReadAllBytes(filePath);
            }
        }

        const int MaxTransferCount = 16;
        const int MaxTransferCountPerRecipient = 5;

        public static TimeSpan MaxTransferDuration = new TimeSpan(0, 2, 0);
        
        public delegate void FileTransferDelegate(FileTransferOut fileStreamReceiver);
        public FileTransferDelegate OnStarted;
        public FileTransferDelegate OnEnded;

        private List<FileTransferOut> activeTransfers;

        private int chunkLen;

        private NetPeer peer;

        public List<FileTransferOut> ActiveTransfers
        {
            get { return activeTransfers; }
        }

        public FileSender(NetworkMember networkMember)
        {
            peer = networkMember.netPeer;
            chunkLen = peer.Configuration.MaximumTransmissionUnit - 100;

            activeTransfers = new List<FileTransferOut>();
        }

        public FileTransferOut StartTransfer(NetConnection recipient, FileTransferType fileType, string filePath)
        {
            if (activeTransfers.Count >= MaxTransferCount)
            {
                return null;
            }

            if (activeTransfers.Count(t => t.Connection == recipient) > MaxTransferCountPerRecipient)
            {
                return null;
            }
            
            if (!File.Exists(filePath))
            {
                DebugConsole.ThrowError("Failed to initiate file transfer (file \""+filePath+"\" not found.");
                return null;
            }

            FileTransferOut transfer = null;
            try
            {
                transfer = new FileTransferOut(recipient, fileType, filePath);
                transfer.SequenceChannel = 1;
                while (activeTransfers.Any(t => t.Connection == recipient && t.SequenceChannel == transfer.SequenceChannel))
                {
                    transfer.SequenceChannel++;
                }
                activeTransfers.Add(transfer);
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Failed to initiate file transfer", e);
                return null;
            }

            OnStarted(transfer);

            return transfer;
        }

        public void Update(float deltaTime)
        {
            activeTransfers.RemoveAll(t => t.Connection.Status != NetConnectionStatus.Connected);

            var endedTransfers = activeTransfers.FindAll(t => 
                t.Connection.Status != NetConnectionStatus.Connected ||
                t.Status == FileTransferStatus.Finished ||
                t.Status == FileTransferStatus.Canceled || 
                t.Status == FileTransferStatus.Error);

            foreach (FileTransferOut transfer in endedTransfers)
            {
                activeTransfers.Remove(transfer);
                OnEnded(transfer);
            }

            foreach (FileTransferOut transfer in activeTransfers)
            {
                transfer.WaitTimer -= deltaTime;
                if (transfer.WaitTimer > 0.0f) continue;
                
                if (!transfer.Connection.CanSendImmediately(NetDeliveryMethod.ReliableOrdered, 1)) continue;
                
                transfer.WaitTimer = transfer.Connection.AverageRoundtripTime;

                // send another part of the file
                long remaining = transfer.Data.Length - transfer.SentOffset;
                int sendByteCount = (remaining > chunkLen ? chunkLen : (int)remaining);
                
                NetOutgoingMessage message;

                //first message; send length, chunk length, file name etc
                if (transfer.SentOffset == 0)
                {
                    message = peer.CreateMessage();
                    message.Write((byte)ServerPacketHeader.FILE_TRANSFER);
                    message.Write((byte)FileTransferMessageType.Initiate);
                    message.Write((byte)transfer.FileType);
                    message.Write((ushort)chunkLen);
                    message.Write((ulong)transfer.Data.Length);
                    message.Write(transfer.FileName);
                    transfer.Connection.SendMessage(message, NetDeliveryMethod.ReliableOrdered, transfer.SequenceChannel);

                    transfer.Status = FileTransferStatus.Sending;

                    if (GameSettings.VerboseLogging)
                    {
                        DebugConsole.Log("Sending file transfer initiation message: ");
                        DebugConsole.Log("  File: " + transfer.FileName);
                        DebugConsole.Log("  Size: " + transfer.Data.Length);
                        DebugConsole.Log("  Sequence channel: " + transfer.SequenceChannel);
                    }
                }

                message = peer.CreateMessage(1 + 1 + sendByteCount);
                message.Write((byte)ServerPacketHeader.FILE_TRANSFER);
                message.Write((byte)FileTransferMessageType.Data);

                byte[] sendBytes = new byte[sendByteCount];
                Array.Copy(transfer.Data, transfer.SentOffset, sendBytes, 0, sendByteCount);

                message.Write(sendBytes);

                transfer.Connection.SendMessage(message, NetDeliveryMethod.ReliableOrdered, transfer.SequenceChannel);
                transfer.SentOffset += sendByteCount;

                if (GameSettings.VerboseLogging)
                {
                    DebugConsole.Log("Sending " + sendByteCount + " bytes of the file " + transfer.FileName + " (" + transfer.SentOffset + "/" + transfer.Data.Length + " sent)");
                }

                if (remaining - sendByteCount <= 0)
                {
                    transfer.Status = FileTransferStatus.Finished;
                }
            }
        }

        public void CancelTransfer(FileTransferOut transfer)
        {
            transfer.Status = FileTransferStatus.Canceled;
            activeTransfers.Remove(transfer);

            OnEnded(transfer);

            GameMain.Server.SendCancelTransferMsg(transfer);
        }

        public void ReadFileRequest(NetIncomingMessage inc)
        {
            byte messageType = inc.ReadByte();

            if (messageType == (byte)FileTransferMessageType.Cancel)
            {
                byte sequenceChannel = inc.ReadByte();
                var matchingTransfer = activeTransfers.Find(t => t.Connection == inc.SenderConnection && t.SequenceChannel == sequenceChannel);
                if (matchingTransfer != null) CancelTransfer(matchingTransfer);

                return;
            }

            byte fileType = inc.ReadByte();
            switch (fileType)
            {
                case (byte)FileTransferType.Submarine:
                    string fileName = inc.ReadString();
                    string fileHash = inc.ReadString();
                    var requestedSubmarine = Submarine.SavedSubmarines.Find(s => s.Name == fileName && s.MD5Hash.Hash == fileHash);

                    if (requestedSubmarine != null)
                    {
                        StartTransfer(inc.SenderConnection, FileTransferType.Submarine, requestedSubmarine.FilePath);
                    }
                    break;
                case (byte)FileTransferType.CampaignSave:
                    if (GameMain.GameSession != null)
                    {
                        StartTransfer(inc.SenderConnection, FileTransferType.CampaignSave, GameMain.GameSession.SavePath);
                    }
                    break;
            }
        }

    }
}
