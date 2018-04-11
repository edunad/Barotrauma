using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Barotrauma
{
    public class GUITickBox : GUIComponent
    {
        private GUIFrame box;
        private GUITextBlock text;

        public delegate bool OnSelectedHandler(GUITickBox obj);
        public OnSelectedHandler OnSelected;

        private bool selected;

        public bool Selected
        {
            get { return selected; }
            set 
            { 
                if (value == selected) return;
                selected = value;
                state = (selected) ? ComponentState.Selected : ComponentState.None;

                box.State = state;
            }
        }

        private bool enabled;

        public bool Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                enabled = value;
            }
        }

        public override Rectangle Rect
        {
            get
            {
                return rect;
            }
            set
            {
                base.Rect = value;

                if (box != null) box.Rect = new Rectangle(value.X,value.Y,box.Rect.Width,box.Rect.Height);
                if (text != null) text.Rect = new Rectangle(box.Rect.Right, box.Rect.Y + 2, 20, box.Rect.Height);
            }
        }

        public Color TextColor
        {
            get { return text.TextColor; }
            set { text.TextColor = value; }
        }

        public override Rectangle MouseRect
        {
            get { return ClampMouseRectToParent ? ClampRect(box.Rect) : box.Rect; }
        }

        public override ScalableFont Font
        {
            get
            {
                return base.Font;
            }

            set
            {
                base.Font = value;
                if (text != null) text.Font = value;
            }
        }

        public GUITickBox(Rectangle rect, string label, Alignment alignment, GUIComponent parent)
            : this(rect, label, alignment, GUI.Font, parent)
        {
        }

        public GUITickBox(Rectangle rect, string label, Alignment alignment, ScalableFont font, GUIComponent parent)
            : base(null)
        {
            if (parent != null)
                parent.AddChild(this);

            box = new GUIFrame(rect, Color.DarkGray, "", this);
            box.HoverColor = Color.Gray;
            box.SelectedColor = Color.DarkGray;
            box.CanBeFocused = false;

            GUI.Style.Apply(box, "GUITickBox");
            
            text = new GUITextBlock(new Rectangle(rect.Right, rect.Y, 20, rect.Height), label, "", Alignment.TopLeft, Alignment.Left | Alignment.CenterY, this, false, font);
            GUI.Style.Apply(text, "GUIButtonHorizontal", this);
            
            this.rect = new Rectangle(box.Rect.X, box.Rect.Y, 240, rect.Height);

            Enabled = true;
        }
        
        public override void Update(float deltaTime)
        {
            if (!Visible) return;

            if (MouseOn == this && Enabled)
            {
                box.State = ComponentState.Hover;

                if (PlayerInput.LeftButtonHeld())
                {
                    box.State = ComponentState.Selected;                    
                }

                if (PlayerInput.LeftButtonClicked())
                {
                    Selected = !Selected;
                    if (OnSelected != null) OnSelected(this);
                }
            }
            else
            {
                box.State = ComponentState.None;
            }

            if (selected)
            {
                box.State = ComponentState.Selected;
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;

            DrawChildren(spriteBatch);            
        }
    }
}
