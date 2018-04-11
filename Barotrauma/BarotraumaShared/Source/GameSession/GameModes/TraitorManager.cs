﻿using Barotrauma.Networking;
using System.Collections.Generic;
using System.IO;

namespace Barotrauma
{
    partial class Traitor
    {
        public readonly Character Character;
        public Character TargetCharacter; //TODO: make a modular objective system (similar to crew missions) that allows for things OTHER than assasinations.

        public Traitor(Character character)
        {
            Character = character;
        }

        public void Greet(GameServer server, string codeWords, string codeResponse)
        {
            string greetingMessage = TextManager.Get("TraitorStartMessage").Replace("[targetname]", TargetCharacter.Name);
            string moreAgentsMessage = TextManager.Get("TraitorMoreAgentsMessage")
                .Replace("[codewords]", codeWords)
                .Replace("[coderesponse]", codeResponse);

            if (server.Character != Character)
            {
                var greetingChatMsg = ChatMessage.Create(null, greetingMessage, ChatMessageType.Server, null);
                var moreAgentsChatMsg = ChatMessage.Create(null, moreAgentsMessage, ChatMessageType.Server, null);

                var greetingMsgBox = ChatMessage.Create(null, greetingMessage, ChatMessageType.MessageBox, null);
                var moreAgentsMsgBox = ChatMessage.Create(null, moreAgentsMessage, ChatMessageType.MessageBox, null);
                
                Client client = server.ConnectedClients.Find(c => c.Character == Character);
                GameMain.Server.SendChatMessage(greetingChatMsg, client);
                GameMain.Server.SendChatMessage(moreAgentsChatMsg, client);
                GameMain.Server.SendChatMessage(greetingMsgBox, client);
                GameMain.Server.SendChatMessage(moreAgentsMsgBox, client);
            }

#if CLIENT
            if (server.Character == null)
            {
                new GUIMessageBox(
                    TextManager.Get("NewTraitor"), 
                    TextManager.Get("TraitorStartMessageServer").Replace("[targetname]", TargetCharacter.Name).Replace("[traitorname]", Character.Name));
            }
            else if (server.Character == Character)
            {
                new GUIMessageBox("", greetingMessage);
                new GUIMessageBox("", moreAgentsMessage);

                GameMain.NetworkMember.AddChatMessage(greetingMessage, ChatMessageType.Server);
                GameMain.NetworkMember.AddChatMessage(moreAgentsMessage, ChatMessageType.Server);
                return;
            }
#endif
        }
    }

    partial class TraitorManager
    {
        private static string wordsTxt = Path.Combine("Content", "CodeWords.txt");

        public List<Traitor> TraitorList
        {
            get { return traitorList; }
        }

        private List<Traitor> traitorList = new List<Traitor>();

        public string codeWords, codeResponse;

        public TraitorManager(GameServer server, int traitorCount)
        {
            if (traitorCount < 1) //what why how
            {
                traitorCount = 1;
                DebugConsole.ThrowError("Traitor Manager: TraitorCount somehow ended up less than 1, setting it to 1.");
            }
            Start(server, traitorCount);
        }

        private void Start(GameServer server, int traitorCount)
        {
            if (server == null) return;

            List<Character> characters = new List<Character>(); //ANYONE can be a target.
            List<Character> traitorCandidates = new List<Character>(); //Keep this to not re-pick traitors twice
            foreach (Client client in server.ConnectedClients)
            {
                if (client.Character != null)
                {
                    characters.Add(client.Character);
                    traitorCandidates.Add(client.Character);
                }
            }

            if (server.Character != null)
            {
                characters.Add(server.Character); //Add host character
                traitorCandidates.Add(server.Character);
            }

            if (characters.Count < 2)
            {
                return;
            }

            codeWords = ToolBox.GetRandomLine(wordsTxt) + ", " + ToolBox.GetRandomLine(wordsTxt);
            codeResponse = ToolBox.GetRandomLine(wordsTxt) + ", " + ToolBox.GetRandomLine(wordsTxt);

            while (traitorCount-- >= 0)
            {
                if (traitorCandidates.Count <= 0)
                    break;

                int traitorIndex = Rand.Int(traitorCandidates.Count);
                Character traitorCharacter = traitorCandidates[traitorIndex];
                traitorCandidates.Remove(traitorCharacter);

                //Add them to the list
                traitorList.Add(new Traitor(traitorCharacter));
            }

            //Now that traitors have been decided, let's do objectives in post for deciding things like Document Exchange.
            foreach (Traitor traitor in traitorList)
            {
                Character traitorCharacter = traitor.Character;
                int targetIndex = Rand.Int(characters.Count);
                while (characters[targetIndex] == traitorCharacter) //Cannot target self
                {
                    targetIndex = Rand.Int(characters.Count);
                }

                Character targetCharacter = characters[targetIndex];
                traitor.TargetCharacter = targetCharacter;
                traitor.Greet(server, codeWords, codeResponse);
            }
        }

        public string GetEndMessage()
        {
            if (GameMain.Server == null || traitorList.Count <= 0) return "";

            string endMessage = "";

            foreach (Traitor traitor in traitorList)
            {
                Character traitorCharacter = traitor.Character;
                Character targetCharacter = traitor.TargetCharacter;
                string messageTag;

                if (targetCharacter.IsDead) //Partial or complete mission success
                {
                    if (traitorCharacter.IsDead)
                    {
                        messageTag = "TraitorEndMessageSuccessTraitorDead";
                    }
                    else if (traitorCharacter.LockHands)
                    {
                        messageTag = "TraitorEndMessageSuccessTraitorDetained";
                    }
                    else
                        messageTag = "TraitorEndMessageSuccess";
                }
                else //Partial or complete failure
                {
                    if (traitorCharacter.IsDead)
                    {
                        messageTag = "TraitorEndMessageFailureTraitorDead";
                    }
                    else if (traitorCharacter.LockHands)
                    {
                        messageTag = "TraitorEndMessageFailureTraitorDetained";
                    }
                    else
                    {
                        messageTag = "TraitorEndMessageFailure";
                    }
                }

                endMessage += (TextManager.ReplaceGenderPronouns(TextManager.Get(messageTag), traitorCharacter.Info.Gender) + "\n")
                    .Replace("[traitorname]", traitorCharacter.Name)
                    .Replace("[targetname]", targetCharacter.Name);
            }

            return endMessage;
        }
    }
}
