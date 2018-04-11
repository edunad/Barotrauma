﻿using Lidgren.Network;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Barotrauma.Networking
{
    class FileReceiver
    {
        public class FileTransferIn : IDisposable
        {            
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

            public ulong FileSize
            {
                get;
                set;
            }

            public ulong Received
            {
                get;
                private set;
            }

            public FileTransferType FileType
            {
                get;
                private set;
            }

            public FileTransferStatus Status
            {
                get;
                set;
            }

            public float BytesPerSecond
            {
                get;
                private set;
            }

            public float Progress
            {
                get { return Received / (float)FileSize; }
            }

            public FileStream WriteStream
            {
                get;
                private set;
            }

            public int TimeStarted
            {
                get;
                private set;
            }

            public NetConnection Connection
            {
                get;
                private set;
            }

            public int SequenceChannel;

            public FileTransferIn(NetConnection connection, string filePath, FileTransferType fileType)
            {
                FilePath = filePath;
                FileName = Path.GetFileName(FilePath);
                FileType = fileType;

                Connection = connection;               

                Status = FileTransferStatus.NotStarted;
            }

            public void OpenStream()
            {
                WriteStream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                TimeStarted = Environment.TickCount;
            }

            public void ReadBytes(NetIncomingMessage inc)
            {
                byte[] all = inc.ReadBytes(inc.LengthBytes - inc.PositionInBytes);
                Received += (ulong)all.Length;
                WriteStream.Write(all, 0, all.Length);

                int passed = Environment.TickCount - TimeStarted;
                float psec = passed / 1000.0f;

                if (GameSettings.VerboseLogging)
                {
                    DebugConsole.Log("Received " + all.Length + " bytes of the file " + FileName + " (" + Received + "/" + FileSize + " received)");
                }

                BytesPerSecond = Received / psec;

                Status = Received >= FileSize ? FileTransferStatus.Finished : FileTransferStatus.Receiving;
            }

            private bool disposed = false;
            protected virtual void Dispose(bool disposing)
            {
                if (disposed) return;
                
                if (disposing)
                {
                    if (WriteStream != null)
                    {
                        WriteStream.Flush();
                        WriteStream.Close();
                        WriteStream.Dispose();
                        WriteStream = null;
                    }
                }
                disposed = true;                
            }
            
            public void Dispose()
            {
                Dispose(true);
            }
        }

        const int MaxFileSize = 1000000;
        
        public delegate void TransferInDelegate(FileTransferIn fileStreamReceiver);
        public TransferInDelegate OnFinished;
        public TransferInDelegate OnTransferFailed;

        private List<FileTransferIn> activeTransfers;

        private Dictionary<FileTransferType, string> downloadFolders = new Dictionary<FileTransferType, string>()
        {
            { FileTransferType.Submarine, "Submarines/Downloaded" },
            { FileTransferType.CampaignSave, "Data/Saves/Multiplayer" }
        };

        public List<FileTransferIn> ActiveTransfers
        {
            get { return activeTransfers; }
        }

        public FileReceiver()
        {
            if (GameMain.Server != null)
            {
                throw new InvalidOperationException("Creating a file receiver is not allowed when a server is running.");
            }

            activeTransfers = new List<FileTransferIn>();
        }
        
        public void ReadMessage(NetIncomingMessage inc)
        {
            if (GameMain.Server != null)
            {
                throw new InvalidOperationException("Receiving files when a server is running is not allowed");
            }

            System.Diagnostics.Debug.Assert(!activeTransfers.Any(t => 
                t.Status == FileTransferStatus.Error ||
                t.Status == FileTransferStatus.Canceled ||
                t.Status == FileTransferStatus.Finished), "List of active file transfers contains entires that should have been removed");

            byte transferMessageType = inc.ReadByte();            
            switch (transferMessageType)
            {
                case (byte)FileTransferMessageType.Initiate:
                    var existingTransfer = activeTransfers.Find(t => t.SequenceChannel == inc.SequenceChannel);
                    if (existingTransfer != null)
                    {
                        GameMain.Client.CancelFileTransfer(inc.SequenceChannel);
                        DebugConsole.ThrowError("File transfer error: file transfer initiated on a sequence channel that's already in use");
                        return;
                    }

                    byte fileType   = inc.ReadByte();
                    ushort chunkLen = inc.ReadUInt16();
                    ulong fileSize  = inc.ReadUInt64();
                    string fileName = inc.ReadString();

                    string errorMsg;
                    if (!ValidateInitialData(fileType, fileName, fileSize, out errorMsg))
                    {
                        GameMain.Client.CancelFileTransfer(inc.SequenceChannel);
                        DebugConsole.ThrowError("File transfer failed (" + errorMsg + ")");
                        return;
                    }

                    if (GameSettings.VerboseLogging)
                    {
                        DebugConsole.Log("Received file transfer initiation message: ");
                        DebugConsole.Log("  File: " + fileName);
                        DebugConsole.Log("  Size: " + fileSize);
                        DebugConsole.Log("  Sequence channel: " + inc.SequenceChannel);
                    }

                    string downloadFolder = downloadFolders[(FileTransferType)fileType];

                    if (!Directory.Exists(downloadFolder))
                    {
                        try
                        {
                            Directory.CreateDirectory(downloadFolder);
                        }
                        catch (Exception e)
                        {
                            DebugConsole.ThrowError("Could not start a file transfer: failed to create the folder \"" + downloadFolder + "\".", e);
                            return;
                        }
                    }

                    FileTransferIn newTransfer = new FileTransferIn(inc.SenderConnection, Path.Combine(downloadFolder, fileName), (FileTransferType)fileType);
                    newTransfer.SequenceChannel = inc.SequenceChannel;
                    newTransfer.Status = FileTransferStatus.Receiving;
                    newTransfer.FileSize = fileSize;

                    try
                    {
                        newTransfer.OpenStream();
                    }
                    catch (IOException e)
                    {
                        GameMain.Client.CancelFileTransfer(inc.SequenceChannel);
                        DebugConsole.NewMessage("Failed to initiate a file transfer {" + e.Message + "}", Color.Red);

                        newTransfer.Status = FileTransferStatus.Error;
                        OnTransferFailed(newTransfer);
                        return;
                    }

                    activeTransfers.Add(newTransfer);

                    break;
                case (byte)FileTransferMessageType.Data:
                    var activeTransfer = activeTransfers.Find(t => t.Connection == inc.SenderConnection && t.SequenceChannel == inc.SequenceChannel);
                    if (activeTransfer == null)
                    {
                        GameMain.Client.CancelFileTransfer(inc.SequenceChannel);
                        DebugConsole.ThrowError("File transfer error: received data without a transfer initiation message");
                        return;
                    }

                    if (activeTransfer.Received + (ulong)(inc.LengthBytes - inc.PositionInBytes) > activeTransfer.FileSize)
                    {
                        GameMain.Client.CancelFileTransfer(inc.SequenceChannel);
                        DebugConsole.ThrowError("File transfer error: Received more data than expected");
                        activeTransfer.Status = FileTransferStatus.Error;
                        StopTransfer(activeTransfer);
                        return;
                    }

                    try
                    {
                        activeTransfer.ReadBytes(inc);
                    }
                    catch (Exception e)
                    {
                        GameMain.Client.CancelFileTransfer(inc.SequenceChannel);
                        DebugConsole.ThrowError("File transfer error: " + e.Message);
                        activeTransfer.Status = FileTransferStatus.Error;
                        StopTransfer(activeTransfer, true);
                        return;
                    }

                    if (activeTransfer.Status == FileTransferStatus.Finished)
                    {
                        activeTransfer.Dispose();

                        string errorMessage = "";
                        if (ValidateReceivedData(activeTransfer, out errorMessage))
                        {
                            StopTransfer(activeTransfer);
                            OnFinished(activeTransfer);
                        }
                        else
                        {
                            new GUIMessageBox("File transfer aborted", errorMessage);

                            activeTransfer.Status = FileTransferStatus.Error;
                            StopTransfer(activeTransfer, true);
                        }
                    }

                    break;
                case (byte)FileTransferMessageType.Cancel:
                    byte sequenceChannel = inc.ReadByte();
                    var matchingTransfer = activeTransfers.Find(t => t.Connection == inc.SenderConnection && t.SequenceChannel == sequenceChannel);
                    if (matchingTransfer != null)
                    {
                        new GUIMessageBox("File transfer cancelled", "The server has cancelled the transfer of the file \"" + matchingTransfer.FileName + "\".");
                        StopTransfer(matchingTransfer);
                    }
                    break;
            }
        }

        private bool ValidateInitialData(byte type, string fileName, ulong fileSize, out string errorMessage)
        {
            errorMessage = "";

            if (fileSize > MaxFileSize)
            {
                errorMessage = "File too large (" + MathUtils.GetBytesReadable((long)fileSize) + ")";
                return false;
            }

            if (!Enum.IsDefined(typeof(FileTransferType), (int)type))
            {
                errorMessage = "Unknown file type";
                return false;
            }

            if (string.IsNullOrEmpty(fileName) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) > -1)
            {
                errorMessage = "Illegal characters in file name ''" + fileName + "''";
                return false;
            }

            switch (type)
            {
                case (byte)FileTransferType.Submarine:
                    if (Path.GetExtension(fileName) != ".sub")
                    {
                        errorMessage = "Wrong file extension ''" + Path.GetExtension(fileName) + "''! (Expected .sub)";
                        return false;
                    }
                    break;
                case (byte)FileTransferType.CampaignSave:
                    if (Path.GetExtension(fileName) != ".save")
                    {
                        errorMessage = "Wrong file extension ''" + Path.GetExtension(fileName) + "''! (Expected .save)";
                        return false;
                    }
                    break;
            }

            return true;
        }

        private bool ValidateReceivedData(FileTransferIn fileTransfer, out string ErrorMessage)
        {
            ErrorMessage = "";
            switch (fileTransfer.FileType)
            {
                case FileTransferType.Submarine:
                    Stream stream = null;

                    try
                    {
                        stream = SaveUtil.DecompressFiletoStream(fileTransfer.FilePath);
                    }
                    catch (Exception e)
                    {
                        ErrorMessage = "Loading received submarine ''" + fileTransfer.FileName + "'' failed! {" + e.Message + "}";
                        return false;
                    }

                    if (stream == null)
                    {
                        ErrorMessage = "Decompressing received submarine file''" + fileTransfer.FilePath + "'' failed!";
                        return false;
                    }

                    try
                    {
                        stream.Position = 0;

                        XmlReaderSettings settings = new XmlReaderSettings();
                        settings.DtdProcessing = DtdProcessing.Prohibit;
                        settings.IgnoreProcessingInstructions = true;

                        using (var reader = XmlReader.Create(stream, settings))
                        {
                            while (reader.Read());
                        }
                    }
                    catch
                    {
                        stream.Close();
                        stream.Dispose();

                        ErrorMessage = "Parsing file ''" + fileTransfer.FilePath + "'' failed! The file may not be a valid submarine file.";
                        return false;
                    }

                    stream.Close();
                    stream.Dispose();
                    break;
                case FileTransferType.CampaignSave:
                    //TODO: verify that the received file is a valid save file
                    break;
            }

            return true;
        }
        
        public void StopTransfer(FileTransferIn transfer, bool deleteFile = false)
        {
            if (transfer.Status != FileTransferStatus.Finished && 
                transfer.Status != FileTransferStatus.Error)
            {
                transfer.Status = FileTransferStatus.Canceled;
            }

            if (activeTransfers.Contains(transfer)) activeTransfers.Remove(transfer);
            transfer.Dispose();

            if (deleteFile && File.Exists(transfer.FilePath))
            {
                try
                {
                    File.Delete(transfer.FilePath);
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Failed to delete file \"" + transfer.FilePath + "\" (" + e.Message + ")");
                }
            }
        }
    }
}
