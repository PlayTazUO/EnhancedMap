using System;
using EnhancedMap.Core.Network;

namespace EnhancedMap.Core.MapObjects
{
    public class PlayerObject : UserObject
    {
        public PlayerObject(string name) : base(name)
        {
            LifeTime = TimeSpan.FromMilliseconds(-1);
        }

        public void SetName(string name)
        {
            Name = name;
        }

        /// <summary>
        ///     With the UO client interop removed, the local player is driven exclusively by the
        ///     network/assistant server (see PacketHandlers). This method is kept so the render loop
        ///     keeps a stable entry point.
        /// </summary>
        public bool Update()
        {
            return false;
        }

        public void SendData()
        {
            byte flags = 0x00;

            if (IsPoisoned)
                flags = (byte) FLAGS_PROPERTY.POISONED;
            if (IsYellowHits)
                flags = (byte) FLAGS_PROPERTY.YELLOWHITS;
            if (IsParalyzed)
                flags = (byte) FLAGS_PROPERTY.PARALYZED;
            if (IsDead)
                flags = (byte) FLAGS_PROPERTY.DEAD;
            if (IsHidden)
                flags = (byte) FLAGS_PROPERTY.HIDDEN;

            if (NetworkManager.SocketClient.IsConnected)
            {
                NetworkManager.SocketClient.Send(new PPlayerData((ushort) Position.X, (ushort) Position.Y, Map, Hits.Min, Stamina.Min, Mana.Min, Hits.Max, Stamina.Max, Mana.Max, flags,
                    /*unused*/ false, InPanic, Hue.Color.ToArgb(), Font.Name, Font.Size, (byte) Font.Style));
            }
        }

        public void Reset()
        {
            if (IsPoisoned)
                IsPoisoned = false;
            if (IsYellowHits)
                IsYellowHits = false;
            if (IsParalyzed)
                IsParalyzed = false;
            if (IsDead)
                IsDead = false;
            if (IsHidden)
                IsHidden = false;

            if (Hits.Min != 0 || Hits.Max != 0)
                Hits.Reset();
            if (Mana.Min != 0 || Mana.Max != 0)
                Mana.Reset();
            if (Stamina.Min != 0 || Stamina.Max != 0)
                Stamina.Reset();

            if (Map != Global.FirstValidFacet)
                Map = (byte) Global.FirstValidFacet;
            if (Position.X != 0 || Position.Y != 0)
                UpdatePosition(0, 0);
        }
    }
}
