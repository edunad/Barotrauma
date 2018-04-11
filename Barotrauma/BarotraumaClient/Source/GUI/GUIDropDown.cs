﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Barotrauma
{
    public class GUIDropDown : GUIComponent
    {
        public delegate bool OnSelectedHandler(GUIComponent selected, object obj = null);
        public OnSelectedHandler OnSelected;
        public OnSelectedHandler OnDropped;

        private GUIButton button;
        private GUIListBox listBox;

        public bool Dropped { get; set; }

        public object SelectedItemData
        {
            get
            {
                if (listBox.Selected == null) return null;
                return listBox.Selected.UserData;
            }
        }

        public bool Enabled
        {
            get { return listBox.Enabled; }
            set { listBox.Enabled = value; }
        }

        public GUIComponent Selected
        {
            get { return listBox.Selected; }
        }

        public GUIListBox ListBox
        {
            get { return listBox; }
        }

        public object SelectedData
        {
            get
            {
                return (listBox.Selected == null) ? null : listBox.Selected.UserData;
            }
        }

        public int SelectedIndex
        {
            get
            {
                if (listBox.Selected == null) return -1;
                return listBox.children.FindIndex(x => x == listBox.Selected);
            }
        }

        public override string ToolTip
        {
            get
            {
                return base.ToolTip;
            }
            set
            {
                base.ToolTip    = value;
                button.ToolTip  = value;
                listBox.ToolTip = value;
            }
        }


        public override Rectangle Rect
        {
            get
            {
                return base.Rect;
            }

            set
            {
                Point moveAmount = value.Location - rect.Location;
                base.Rect = value;

                button.Rect = new Rectangle(button.Rect.Location + moveAmount, button.Rect.Size);
                listBox.Rect = new Rectangle(listBox.Rect.Location + moveAmount, listBox.Rect.Size);
            }
        }

        public GUIDropDown(Rectangle rect, string text, string style, GUIComponent parent = null)
            : this(rect, text, style, Alignment.TopLeft, parent)
        {
        }

        public GUIDropDown(Rectangle rect, string text, string style, Alignment alignment, GUIComponent parent = null)
            : base(style)
        {
            this.rect = rect;

            if (parent != null) parent.AddChild(this);

            button = new GUIButton(this.rect, text, Color.White, alignment, Alignment.CenterLeft, "GUIDropDown", null);
            GUI.Style.Apply(button, style, this);

            button.OnClicked = OnClicked;

            listBox = new GUIListBox(new Rectangle(this.rect.X, this.rect.Bottom, this.rect.Width, 200), style, null);
            listBox.OnSelected = SelectItem;
        }

        public override void AddChild(GUIComponent child)
        {
            listBox.AddChild(child);
        }

        public void AddItem(string text, object userData = null, string toolTip = "")
        {
            GUITextBlock textBlock = new GUITextBlock(new Rectangle(0,0,0,20), text, "ListBoxElement", Alignment.TopLeft, Alignment.CenterLeft, listBox);
            textBlock.UserData = userData;
            textBlock.ToolTip = toolTip;
        }

        public override void ClearChildren()
        {
            listBox.ClearChildren();
        }

        public List<GUIComponent> GetChildren()
        {
            return listBox.children;
        }

        private bool SelectItem(GUIComponent component, object obj)
        {
            GUITextBlock textBlock = component as GUITextBlock;
            if (textBlock == null)
            {
                textBlock = component.GetChild<GUITextBlock>();
                if (textBlock == null) return false;
            }

            button.Text = textBlock.Text;
            Dropped = false;

            if (OnSelected != null) OnSelected(component, component.UserData);

            return true;
        }

        public void SelectItem(object userData)
        {
            //GUIComponent child = listBox.children.FirstOrDefault(c => c.UserData == userData);

            //if (child == null) return;

            listBox.Select(userData);

            //SelectItem(child, userData);
        }

        public void Select(int index)
        {
            listBox.Select(index);
        }
       

        private bool wasOpened;

        private bool OnClicked(GUIComponent component, object obj)
        {
            if (wasOpened) return false;
            
            wasOpened = true;
            Dropped = !Dropped;

            if (Dropped)
            {
                if (Enabled) OnDropped?.Invoke(this, userData);
                if (parent.children[parent.children.Count - 1] != this)
                {
                    parent.children.Remove(this);
                    parent.children.Add(this);
                }
            }

            return true;
        }

        public override void AddToGUIUpdateList()
        {
            base.AddToGUIUpdateList();
            button.AddToGUIUpdateList();
            if (Dropped) listBox.AddToGUIUpdateList();
        }

        public override void Update(float deltaTime)
        {
            if (!Visible) return;

            wasOpened = false;

            base.Update(deltaTime);

            if (Dropped && PlayerInput.LeftButtonClicked())
            {
                Rectangle listBoxRect = listBox.Rect;
                listBoxRect.Width += 20;
                if (!listBoxRect.Contains(PlayerInput.MousePosition) && !button.Rect.Contains(PlayerInput.MousePosition))
                {
                    Dropped = false;
                }
            }
            
            button.Update(deltaTime);

            if (Dropped) listBox.Update(deltaTime);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;

            base.Draw(spriteBatch);

            button.Draw(spriteBatch);

            if (!Dropped) return;
            listBox.Draw(spriteBatch);
        }
    }
}
