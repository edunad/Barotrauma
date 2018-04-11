﻿using Lidgren.Network;
using System;

namespace Barotrauma.Networking
{
    partial class ChatMessage
    {
        public void ClientWrite(NetOutgoingMessage msg)
        {
            msg.Write((byte)ClientNetObject.CHAT_MESSAGE);
            msg.Write(NetStateID);
            msg.Write(Text);
        }

        public static void ClientRead(NetIncomingMessage msg)
        {
            UInt16 ID = msg.ReadUInt16();
            ChatMessageType type = (ChatMessageType)msg.ReadByte();
            string txt = msg.ReadString();

            string senderName = msg.ReadString();
            Character senderCharacter = null;
            bool hasSenderCharacter = msg.ReadBoolean();
            if (hasSenderCharacter)
            {
                senderCharacter = Entity.FindEntityByID(msg.ReadUInt16()) as Character;
                if (senderCharacter != null)
                {
                    senderName = senderCharacter.Name;
                }
            }

            if (NetIdUtils.IdMoreRecent(ID, LastID))
            {
                if (type == ChatMessageType.MessageBox)
                {
                    new GUIMessageBox("", txt);
                }
                else if (type == ChatMessageType.Console)
                {
                    DebugConsole.NewMessage(txt, MessageColor[(int)ChatMessageType.Console]);
                }
                else
                {
                    GameMain.Client.AddChatMessage(txt, type, senderName, senderCharacter);
                }
                LastID = ID;
            }
        }
    }
}
