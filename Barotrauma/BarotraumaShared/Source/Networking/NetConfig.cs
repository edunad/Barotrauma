﻿namespace Barotrauma.Networking
{
    static class NetConfig
    {
        public const int DefaultPort = 14242;
        
        public const int MaxPlayers = 16;

        public static string MasterServerUrl = GameMain.Config.MasterServerUrl;

        //if a Character is further than this from the sub, the server will ignore it
        //(in display units)
        public const float CharacterIgnoreDistance = 20000.0f;
        public const float CharacterIgnoreDistanceSqr = CharacterIgnoreDistance * CharacterIgnoreDistance;

        //how much the physics body of an item has to move until the server 
        //send a position update to clients (in sim units)
        public const float ItemPosUpdateDistance = 2.0f;
        
        public const float DeleteDisconnectedTime = 10.0f;        
    }
}
