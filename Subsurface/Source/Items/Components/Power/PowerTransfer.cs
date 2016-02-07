﻿using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Globalization;

namespace Barotrauma.Items.Components
{
    class PowerTransfer : Powered
    {
        static float fullPower;
        static float fullLoad;

        //private bool updated;

        private int updateTimer;
        
        const float FireProbability = 0.15f;

        //affects how fast changes in power/load are carried over the grid
        static float inertia = 5.0f;

        static List<Powered> connectedList = new List<Powered>();

        private float powerLoad;

        public float PowerLoad
        {
            get { return powerLoad; }
        }

        public PowerTransfer(Item item, XElement element)
            : base(item, element)
        {
            IsActive = true;
        }

        public override void Update(float deltaTime, Camera cam) 
        {
            //reset and recalculate the power generated/consumed
            //by the constructions connected to the grid
            fullPower = 0.0f;
            fullLoad = 0.0f;
            connectedList.Clear();

            if (updateTimer > 0)
            {
                updateTimer--;
                return;
            }
            
            CheckJunctions(deltaTime);
            updateTimer = 0;

            foreach (Powered p in connectedList)
            {
                PowerTransfer pt = p as PowerTransfer;
                if (pt == null) continue;
                
                pt.powerLoad += (fullLoad - pt.powerLoad) / inertia;
                pt.currPowerConsumption += (-fullPower - pt.currPowerConsumption) / inertia;
                pt.Item.SendSignal("", "power", fullPower / Math.Max(fullLoad, 1.0f));

                //damage the item if voltage is too high
                if (-pt.currPowerConsumption < Math.Max(pt.powerLoad * Rand.Range(1.95f,2.05f), 200.0f)) continue;

                
                float prevCondition = pt.item.Condition;
                pt.item.Condition -= deltaTime * 10.0f;

                if (pt.item.Condition <= 0.0f && prevCondition > 0.0f)
                {
                    sparkSounds[Rand.Int(sparkSounds.Length)].Play(1.0f, 600.0f, pt.item.WorldPosition);

                    Vector2 baseVel = Rand.Vector(300.0f);
                    for (int i = 0; i < 10; i++)
                    {
                        var particle = GameMain.ParticleManager.CreateParticle("spark", pt.item.WorldPosition,
                            baseVel + Rand.Vector(100.0f), 0.0f, item.CurrentHull);

                        if (particle != null) particle.Size *= Rand.Range(0.5f, 1.0f);
                    }

                    if (FireProbability > 0.0f && Rand.Int((int)(1.0f / FireProbability)) == 1)
                    {
                        new FireSource(pt.item.WorldPosition);
                    }
                }
                  
            }
        }

        public override bool Pick(Character picker)
        {
            return picker != null;
        }

        //a recursive function that goes through all the junctions and adds up
        //all the generated/consumed power of the constructions connected to the grid
        private void CheckJunctions(float deltaTime)
        {
            updateTimer = 1;
            connectedList.Add(this);

            ApplyStatusEffects(ActionType.OnActive, deltaTime, null);

            List<Connection> connections = item.Connections;
            if (connections == null) return;

            foreach (Connection c in connections)
            {
                if (!c.IsPower) continue;
                foreach (Connection recipient in c.Recipients)
                {
                    if (recipient == null || !c.IsPower) continue;

                    Item it = recipient.Item;
                    if (it == null) continue;

                    //if (it.Updated) continue;

                    Powered powered = it.GetComponent<Powered>();
                    if (powered == null) continue;

                    PowerTransfer powerTransfer = powered as PowerTransfer;
                    PowerContainer powerContainer = powered as PowerContainer;
                    if (powerTransfer != null)
                    {
                        if (powerTransfer.updateTimer>0) continue;
                        powerTransfer.CheckJunctions(deltaTime);
                    }
                    else if (powerContainer != null)
                    {
                        if (recipient.Name == "power_in")
                        {
                            fullLoad += powerContainer.CurrPowerConsumption;
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
                            fullLoad += powered.CurrPowerConsumption;
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
        
        public override void DrawHUD(SpriteBatch spriteBatch, Character character)
        {
            int x = GuiFrame.Rect.X;
            int y = GuiFrame.Rect.Y;

            GuiFrame.Draw(spriteBatch);

            spriteBatch.DrawString(GUI.Font, "Power: " + (int)(-currPowerConsumption) + " kW", new Vector2(x + 30, y + 30), Color.White);
            spriteBatch.DrawString(GUI.Font, "Load: " + (int)powerLoad + " kW", new Vector2(x + 30, y + 100), Color.White);
        }

        public override void ReceiveSignal(string signal, Connection connection, Item sender, float power)
        {
            base.ReceiveSignal(signal, connection, sender, power);

            if (connection.Name.Length > 5 && connection.Name.Substring(0, 6).ToLower() == "signal")
            {
                connection.SendSignal(signal, sender, 0.0f);
            }
        }

    }
}
