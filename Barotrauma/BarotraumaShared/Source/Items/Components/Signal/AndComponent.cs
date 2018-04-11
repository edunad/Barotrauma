﻿using System;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    class AndComponent : ItemComponent
    {
        protected string output, falseOutput;

        //an array to keep track of how long ago a non-zero signal was received on both inputs
        protected float[] timeSinceReceived;

        //the output is sent if both inputs have received a signal within the timeframe
        protected float timeFrame;
        
        [InGameEditable, Serialize(0.0f, true)]
        public float TimeFrame
        {
            get { return timeFrame; }
            set
            {
                timeFrame = Math.Max(0.0f, value);
            }
        }

        [InGameEditable, Serialize("1", true)]
        public string Output
        {
            get { return output; }
            set { output = value; }
        }

        [InGameEditable, Serialize("", true)]
        public string FalseOutput
        {
            get { return falseOutput; }
            set { falseOutput = value; }
        }
        
        public AndComponent(Item item, XElement element)
            : base (item, element)
        {
            timeSinceReceived = new float[] { Math.Max(timeFrame*2.0f,0.1f), Math.Max(timeFrame*2.0f, 0.1f) };

            //output = "1";
        }

        public override void Update(float deltaTime, Camera cam)
        {
            bool sendOutput = true;
            for (int i = 0; i<timeSinceReceived.Length; i++)
            {                
                if (timeSinceReceived[i] > timeFrame) sendOutput = false;
                timeSinceReceived[i] += deltaTime;
            }

            string signalOut = sendOutput ? output : falseOutput;
            if (string.IsNullOrEmpty(signalOut)) return;

            item.SendSignal(0, signalOut, "signal_out", null);
        }

        public override void ReceiveSignal(int stepsTaken, string signal, Connection connection, Item source, Character sender, float power=0.0f)
        {
            switch (connection.Name)
            {
                case "signal_in1":
                    if (signal == "0") return;
                    timeSinceReceived[0] = 0.0f;
                    IsActive = true;
                    break;
                case "signal_in2":
                    if (signal == "0") return;
                    timeSinceReceived[1] = 0.0f;
                    IsActive = true;
                    break;
                case "set_output":
                    output = signal;
                    break;
            }
        }
    }
}
