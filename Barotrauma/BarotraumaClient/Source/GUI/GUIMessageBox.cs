﻿using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Barotrauma
{
    public class GUIMessageBox : GUIFrame
    {
        public static List<GUIComponent> MessageBoxes = new List<GUIComponent>();

        public const int DefaultWidth = 400, DefaultHeight = 250;
        
        public GUIButton[] Buttons;

        public static GUIComponent VisibleBox
        {
            get { return MessageBoxes.Count == 0 ? null : MessageBoxes[0]; }
        }

        public GUIFrame InnerFrame
        {
            get { return children[0] as GUIFrame; }
        }

        public string Text
        {
            get { return (children[0].children[1] as GUITextBlock).Text; }
            set { (children[0].children[1] as GUITextBlock).Text = value; }
        }

        public GUIMessageBox(string headerText, string text)
            : this(headerText, text, new string[] {"OK"}, DefaultWidth, 0)
        {
            this.Buttons[0].OnClicked = Close;
        }

        public GUIMessageBox(string headerText, string text, int width, int height)
            : this(headerText, text, new string[] { "OK" }, width, height)
        {
            this.Buttons[0].OnClicked = Close;
        }

        public GUIMessageBox(string headerText, string text, string[] buttons, int width = DefaultWidth, int height = 0, Alignment textAlignment = Alignment.TopLeft, GUIComponent parent = null)
            : base(new Rectangle(0, 0, GameMain.GraphicsWidth, GameMain.GraphicsHeight),
                Color.Black * 0.5f, Alignment.TopLeft, null, parent)
        {
            int headerHeight = 30;

            var frame = new GUIFrame(new Rectangle(0, 0, width, height), null, Alignment.Center, "", this);
            GUI.Style.Apply(frame, "", this);
            
            if (height == 0)
            {
                string wrappedText = ToolBox.WrapText(text, frame.Rect.Width - frame.Padding.X - frame.Padding.Z, GUI.Font);
                string[] lines = wrappedText.Split('\n');
                foreach (string line in lines)
                {
                    height += (int)GUI.Font.MeasureString(line).Y;
                }
                height += string.IsNullOrWhiteSpace(headerText) ? 220 : 220 - headerHeight;
            }
            frame.Rect = new Rectangle(frame.Rect.X, GameMain.GraphicsHeight / 2 - height/2, frame.Rect.Width, height);

            var header = new GUITextBlock(new Rectangle(0, 0, 0, headerHeight), headerText, null, null, textAlignment, "", frame, true);
            GUI.Style.Apply(header, "", this);            

            if (!string.IsNullOrWhiteSpace(text))
            {
                var textBlock = new GUITextBlock(new Rectangle(0, string.IsNullOrWhiteSpace(headerText) ? 0 : headerHeight, 0, height - 70), text,
                    null, null, textAlignment, "", frame, true);
                GUI.Style.Apply(textBlock, "", this);
            }

            int x = 0;
            this.Buttons = new GUIButton[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                this.Buttons[i] = new GUIButton(new Rectangle(x, 0, 150, 30), buttons[i], Alignment.Left | Alignment.Bottom, "", frame);

                x += this.Buttons[i].Rect.Width + 20;
            }

            MessageBoxes.Add(this);
        }

        public void Close()
        {
            if (parent != null) parent.RemoveChild(this);
            if (MessageBoxes.Contains(this)) MessageBoxes.Remove(this);
        }

        public bool Close(GUIButton button, object obj)
        {
            Close();

            return true;
        }

        public static void CloseAll()
        {
            MessageBoxes.Clear();
        }
    }
}
