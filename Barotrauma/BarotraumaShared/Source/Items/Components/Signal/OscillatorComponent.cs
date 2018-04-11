﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace Barotrauma.Items.Components
{
    class OscillatorComponent : ItemComponent
    {
        public enum WaveType
        {
            Pulse,
            Sine,
            Square,
        }

        private float frequency;

        private float phase;

        [InGameEditable, Serialize(WaveType.Pulse, true)]
        public WaveType OutputType
        {
            get;
            set;
        }

        [InGameEditable, Serialize(1.0f, true)]
        public float Frequency
        {
            get { return frequency; }
            set
            {
                //capped to 240 Hz (= 4 signals per frame) to prevent players 
                //from wrecking the performance by setting the value too high
                frequency = MathHelper.Clamp(value, 0.0f, 240.0f);
            }
        }

        public OscillatorComponent(Item item, XElement element) : 
            base(item, element)
        {
            IsActive = true;
        }

        public override void Update(float deltaTime, Camera cam)
        {
            switch (OutputType)
            {
                case WaveType.Pulse:
                    if (frequency <= 0.0f) return;

                    phase += deltaTime;
                    float pulseInterval = 1.0f / frequency;
                    while (phase >= pulseInterval)
                    {
                        item.SendSignal(0, "1", "signal_out", null);
                        phase -= pulseInterval;
                    }
                    break;
                case WaveType.Square:
                    phase = (phase + deltaTime * frequency) % 1.0f;
                    item.SendSignal(0, phase < 0.5f ? "0" : "1", "signal_out", null);
                    break;
                case WaveType.Sine:
                    phase = (phase + deltaTime * frequency) % 1.0f;
                    item.SendSignal(0, Math.Sin(phase * MathHelper.TwoPi).ToString(CultureInfo.InvariantCulture), "signal_out", null);
                    break;
            }
        }

        public override void ReceiveSignal(int stepsTaken, string signal, Connection connection, Item source, Character sender, float power = 0.0f)
        {
            switch (connection.Name)
            {
                case "set_frequency":
                case "frequency_in":
                    float newFrequency;
                    if (float.TryParse(signal, out newFrequency))
                    {
                        Frequency = newFrequency;
                    }
                    IsActive = true;
                    break;
                case "set_outputtype":
                case "set_wavetype":
                    WaveType newOutputType;
                    if (Enum.TryParse(signal, out newOutputType))
                    {
                        OutputType = newOutputType;
                    }
                    break;
            }
        }
    }
}
