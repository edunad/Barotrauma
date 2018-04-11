﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class PowerTransfer : Powered
    {
        static float fullPower;
        static float fullLoad;

        private int updateCount;

        const float FireProbability = 0.15f;

        //affects how fast changes in power/load are carried over the grid
        static float inertia = 5.0f;

        private static HashSet<Powered> connectedList = new HashSet<Powered>();
        private List<Connection> powerConnections;
        public List<Connection> PowerConnections
        {
            get
            {
                return powerConnections;
            }
        }


        private Dictionary<Connection, bool> connectionDirty = new Dictionary<Connection, bool>();

        //a list of connections a given connection is connected to, either directly or via other power transfer components
        private Dictionary<Connection, HashSet<Connection>> connectedRecipients = new Dictionary<Connection, HashSet<Connection>>();

        private float powerLoad;

        private bool isBroken;

        public float PowerLoad
        {
            get { return powerLoad; }
        }

        //can the component transfer power
        private bool canTransfer;
        public bool CanTransfer
        {
            get { return canTransfer; }
            set
            {
                if (canTransfer == value) return;
                canTransfer = value;
                SetAllConnectionsDirty();
            }
        }

        public override bool IsActive
        {
            get
            {
                return base.IsActive;
            }

            set
            {
                if (base.IsActive == value) return;
                base.IsActive = value;
                powerLoad = 0.0f;
                currPowerConsumption = 0.0f;

                SetAllConnectionsDirty();
                if (!base.IsActive)
                {
                    //we need to refresh the connections here because Update won't be called on inactive components
                    RefreshConnections();
                }
            }
        }

        public PowerTransfer(Item item, XElement element)
            : base(item, element)
        {
            IsActive = true;
            canTransfer = true;
        }

        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            base.UpdateBroken(deltaTime, cam);

            if (!isBroken)
            {
                powerLoad = 0.0f;
                currPowerConsumption = 0.0f;
                SetAllConnectionsDirty();
                RefreshConnections();
                isBroken = true;
            }
        }

        public override void Update(float deltaTime, Camera cam)
        {
            RefreshConnections();
            if (!CanTransfer) return;

            if (isBroken)
            {
                SetAllConnectionsDirty();
                isBroken = false;
            }
            
            if (updateCount > 0)
            {
                //this junction box has already been updated this frame
                updateCount--;
                return;
            }

            //reset and recalculate the power generated/consumed
            //by the constructions connected to the grid
            fullPower = 0.0f;
            fullLoad = 0.0f;

            connectedList.Clear();

            updateCount = 0;
            CheckJunctions(deltaTime);

            foreach (Powered p in connectedList)
            {
                PowerTransfer pt = p as PowerTransfer;
                if (pt == null || pt.updateCount == 0) continue;

                if (pt is RelayComponent != this is RelayComponent) continue;

                pt.powerLoad += (fullLoad - pt.powerLoad) / inertia;
                pt.currPowerConsumption += (-fullPower - pt.currPowerConsumption) / inertia;

                float voltage = fullPower / Math.Max(fullLoad, 1.0f);
                if (this is RelayComponent) voltage = Math.Min(voltage, 1.0f);

                pt.Item.SendSignal(0, "", "power", null, voltage);
                pt.Item.SendSignal(0, "", "power_out", null, voltage);

                //damage the item if voltage is too high 
                //(except if running as a client)
                if (GameMain.Client != null) continue;

                //relays don't blow up if the power is higher than load, only if the output is high enough 
                //(i.e. enough power passing through the relay)
                if (this is RelayComponent) continue;
                if (-pt.currPowerConsumption < Math.Max(pt.powerLoad * Rand.Range(1.9f, 2.1f), 200.0f)) continue;

                float prevCondition = pt.item.Condition;
                pt.item.Condition -= deltaTime * 10.0f;

                if (pt.item.Condition <= 0.0f && prevCondition > 0.0f)
                {
#if CLIENT
                    sparkSounds[Rand.Int(sparkSounds.Length)].Play(1.0f, 600.0f, pt.item.WorldPosition);

                    Vector2 baseVel = Rand.Vector(300.0f);
                    for (int i = 0; i < 10; i++)
                    {
                        var particle = GameMain.ParticleManager.CreateParticle("spark", pt.item.WorldPosition,
                            baseVel + Rand.Vector(100.0f), 0.0f, item.CurrentHull);

                        if (particle != null) particle.Size *= Rand.Range(0.5f, 1.0f);
                    }
#endif

                    if (FireProbability > 0.0f && Rand.Int((int)(1.0f / FireProbability)) == 1)
                    {
                        new FireSource(pt.item.WorldPosition);
                    }
                }
            }

            updateCount = 0;
        }

        public override bool Pick(Character picker)
        {
            return picker != null;
        }

        private void RefreshConnections()
        {
            var connections = item.Connections;
            foreach (Connection c in connections)
            {
                if (!connectionDirty.ContainsKey(c))
                {
                    connectionDirty[c] = true;
                }
                else if (!connectionDirty[c])
                {
                    continue;
                }

                HashSet<Connection> connected = new HashSet<Connection>();
                if (!connectedRecipients.ContainsKey(c))
                {
                    connectedRecipients.Add(c, connected);
                }
                else
                {
                    //mark all previous recipients as dirty
                    foreach (Connection recipient in connectedRecipients[c])
                    {
                        var pt = recipient.Item.GetComponent<PowerTransfer>();
                        if (pt != null) pt.connectionDirty[recipient] = true;
                    }
                }

                //find all connections that are connected to this one (directly or via another PowerTransfer)
                connected.Add(c);
                GetConnected(c, connected);
                connectedRecipients[c] = connected;

                //go through all the PowerTransfers and we're connected to and set their connections to match the ones we just calculated
                //(no need to go through the recursive GetConnected method again)
                foreach (Connection recipient in connected)
                {
                    var recipientPowerTransfer = recipient.Item.GetComponent<PowerTransfer>();
                    if (recipientPowerTransfer == null) continue;

                    if (!connectedRecipients.ContainsKey(recipient))
                    {
                        connectedRecipients.Add(recipient, connected);
                    }

                    recipientPowerTransfer.connectedRecipients[recipient] = connected;
                    recipientPowerTransfer.connectionDirty[recipient] = false;
                }
            }
        }

        //Finds all the connections that can receive a signal sent into the given connection and stores them in the hashset.
        private void GetConnected(Connection c, HashSet<Connection> connected)
        {
            var recipients = c.Recipients;

            foreach (Connection recipient in recipients)
            {
                if (recipient == null || connected.Contains(recipient)) continue;

                Item it = recipient.Item;
                if (it == null || it.Condition <= 0.0f) continue;

                connected.Add(recipient);

                var powerTransfer = it.GetComponent<PowerTransfer>();
                if (powerTransfer != null && powerTransfer.CanTransfer && powerTransfer.IsActive)
                {
                    GetConnected(recipient, connected);
                }
            }
        }

        //a recursive function that goes through all the junctions and adds up
        //all the generated/consumed power of the constructions connected to the grid
        private void CheckJunctions(float deltaTime, bool increaseUpdateCount = true, bool inputOnly = false)
        {
            if (increaseUpdateCount)
            {
                updateCount = 1;
            }
            connectedList.Add(this);

            ApplyStatusEffects(ActionType.OnActive, deltaTime, null);

            foreach (Connection c in PowerConnections)
            {
                var recipients = c.Recipients;

                foreach (Connection recipient in recipients)
                {
                    if (recipient == null) continue;

                    Item it = recipient.Item;
                    if (it == null) continue;

                    if (it.Condition <= 0.0f) continue;

                    foreach (Powered powered in it.GetComponents<Powered>())
                    {
                        if (powered == null || !powered.IsActive) continue;
                        if (connectedList.Contains(powered)) continue;

                        PowerTransfer powerTransfer = powered as PowerTransfer;
                        if (powerTransfer != null)
                        {
                            if (this is RelayComponent == powerTransfer is RelayComponent)
                            {
                                if (!powerTransfer.CanTransfer) continue;
                                powerTransfer.CheckJunctions(deltaTime, increaseUpdateCount, inputOnly);
                            }
                            else
                            {
                                if (!powerTransfer.CanTransfer) continue;
                                powerTransfer.CheckJunctions(deltaTime, false, !c.IsOutput || inputOnly);
                            }

                            continue;
                        }

                        PowerContainer powerContainer = powered as PowerContainer;
                        if (powerContainer != null)
                        {
                            if (recipient.Name == "power_in")
                            {
                                if (!inputOnly) fullLoad += powerContainer.CurrPowerConsumption;
                            }
                            else
                            {
                                fullPower += powerContainer.CurrPowerOutput;
                            }
                        }
                        else
                        {
                            connectedList.Add(powered);
                            //positive power consumption = the construction requires power -> increase load
                            if (powered.CurrPowerConsumption > 0.0f)
                            {
                                if (!inputOnly) fullLoad += powered.CurrPowerConsumption;
                            }
                            else if (powered.CurrPowerConsumption < 0.0f)
                            //negative power consumption = the construction is a 
                            //generator/battery or another junction box
                            {
                                fullPower -= powered.CurrPowerConsumption;
                            }
                        }
                    }

                }
            }
        }

        public void SetAllConnectionsDirty()
        {
            if (item.Connections == null) return;
            foreach (Connection c in item.Connections)
            {
                connectionDirty[c] = true;
            }
        }

        public void SetConnectionDirty(Connection connection)
        {
            var connections = item.Connections;
            if (connections == null || !connections.Contains(connection)) return;
            connectionDirty[connection] = true;
        }

        public override void OnItemLoaded()
        {
            var connections = Item.Connections;
            powerConnections = connections == null ? new List<Connection>() : connections.FindAll(c => c.IsPower);  
            if (connections == null)
            {
                IsActive = false;
                return;
            }
            SetAllConnectionsDirty();
        }
        
        public override void ReceiveSignal(int stepsTaken, string signal, Connection connection, Item source, Character sender, float power)
        {
            if (connection.IsPower) return;

            base.ReceiveSignal(stepsTaken, signal, connection, source, sender, power);

            if (!connectedRecipients.ContainsKey(connection)) return;

            if (connection.Name.Length > 5 && connection.Name.Substring(0, 6).ToLowerInvariant() == "signal")
            {
                foreach (Connection recipient in connectedRecipients[connection])
                {
                    if (recipient.Item == item || recipient.Item == source) continue;

                    foreach (ItemComponent ic in recipient.Item.components)
                    {
                        //powertransfer components don't need to receive the signal in the pass-through signal connections
                        //because we relay it straight to the connected items without going through the whole chain of junction boxes
                        if (ic is PowerTransfer && connection.Name.Contains("signal")) continue;
                        ic.ReceiveSignal(stepsTaken, signal, recipient, source, sender, 0.0f);
                    }

                    foreach (StatusEffect effect in recipient.effects)
                    {
                        recipient.Item.ApplyStatusEffect(effect, ActionType.OnUse, 1.0f);
                    }
                }
            }
        }

    }
}
