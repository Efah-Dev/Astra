using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Astra {
enum TextTone { Main,Muted,Accent,Warning,Error }
sealed class ThemeComboBox:ComboBox {
 public Palette Colors=Palette.Create(false);
 public ThemeComboBox(){DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=18;}
 protected override void OnDrawItem(DrawItemEventArgs e){if(e.Bounds.Width<=0)return;bool selected=(e.State&DrawItemState.Selected)!=0;Color background=selected?Colors.Hover:Colors.Input;
  using(var brush=new SolidBrush(background))e.Graphics.FillRectangle(brush,e.Bounds);
  string text=e.Index>=0&&e.Index<Items.Count?GetItemText(Items[e.Index]):Text;TextRenderer.DrawText(e.Graphics,text,Font,Rectangle.Inflate(e.Bounds,-3,0),Enabled?Colors.Text:Colors.Disabled,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
 }
 protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ItemHeight=Font.Height+4;}
 protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==0x000F&&IsHandleCreated){using(var g=Graphics.FromHwnd(Handle)){int width=SystemInformation.VerticalScrollBarWidth;var arrow=new Rectangle(Width-width-1,1,width,Height-2);using(var fill=new SolidBrush(Enabled?Colors.Button:Colors.Card))g.FillRectangle(fill,arrow);using(var pen=new Pen(Colors.Border))g.DrawRectangle(pen,0,0,Width-1,Height-1);int x=arrow.Left+arrow.Width/2,y=Height/2;using(var fill=new SolidBrush(Enabled?Colors.Text:Colors.Disabled))g.FillPolygon(fill,new[]{new Point(x-3,y-1),new Point(x+3,y-1),new Point(x,y+2)});}}}
}
sealed class ThemeButton:Button {
 public Palette Colors=Palette.Create(false);bool hovering;
 public ThemeButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
 protected override void OnMouseEnter(EventArgs e){hovering=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hovering=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){bool primary=Equals(Tag,"primary");var t=Colors;Color background=primary?Color.FromArgb(18,113,103):t.Button;
  if(!Enabled)background=t.Card;else if(hovering)background=primary?Color.FromArgb(24,134,121):t.Hover;
  e.Graphics.Clear(background);using(var pen=new Pen(primary&&Enabled?background:t.Border))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
  TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?(primary?Color.White:t.Text):t.Disabled,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),t.Text,background);
 }
}
sealed class ThemeCheckBox:CheckBox {
 public Palette Colors=Palette.Create(false);
 public ThemeCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
 protected override void OnPaint(PaintEventArgs e){var t=Colors;e.Graphics.Clear(BackColor);int side=Math.Max(12,Font.Height-2);var box=new Rectangle(0,(Height-side)/2,side,side);
  using(var brush=new SolidBrush(t.Input))e.Graphics.FillRectangle(brush,box);using(var pen=new Pen(Enabled?t.Muted:t.Border))e.Graphics.DrawRectangle(pen,box);
  if(Checked)using(var pen=new Pen(Enabled?t.Accent:t.Disabled,2)){e.Graphics.DrawLines(pen,new[]{new Point(box.Left+3,box.Top+side/2),new Point(box.Left+side/2-1,box.Bottom-3),new Point(box.Right-2,box.Top+3)});}
  var text=new Rectangle(side+6,0,Math.Max(0,Width-side-6),Height);TextRenderer.DrawText(e.Graphics,Text,Font,text,Enabled?t.TextColor(Tag):t.Disabled,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,text,t.Text,BackColor);
 }
}
sealed class Palette {
 public bool Dark;public Color Window,Card,Input,Text,Muted,Accent,Border,Button,Hover,Warning,Error,Disabled;
 public static Palette Create(bool dark){return dark
  ?new Palette{Dark=true,Window=Color.FromArgb(22,26,32),Card=Color.FromArgb(30,36,44),Input=Color.FromArgb(38,46,56),Text=Color.FromArgb(230,236,243),Muted=Color.FromArgb(170,185,200),Accent=Color.FromArgb(95,213,190),Border=Color.FromArgb(78,93,108),Button=Color.FromArgb(40,49,60),Hover=Color.FromArgb(55,70,82),Warning=Color.FromArgb(236,193,118),Error=Color.FromArgb(255,151,151),Disabled=Color.FromArgb(133,148,164)}
  :new Palette{Window=Color.FromArgb(243,246,249),Card=Color.White,Input=Color.White,Text=Color.FromArgb(29,47,62),Muted=Color.FromArgb(91,109,125),Accent=Color.FromArgb(18,113,103),Border=Color.FromArgb(192,206,216),Button=Color.White,Hover=Color.FromArgb(232,241,240),Warning=Color.FromArgb(133,85,22),Error=Color.Firebrick,Disabled=Color.FromArgb(117,128,139)};
 }
 public static Palette SystemPalette(){
  if(SystemInformation.HighContrast)return new Palette{Window=SystemColors.Window,Card=SystemColors.Window,Input=SystemColors.Window,Text=SystemColors.WindowText,Muted=SystemColors.WindowText,Accent=SystemColors.HotTrack,Border=SystemColors.WindowText,Button=SystemColors.Control,Hover=SystemColors.Highlight,Warning=SystemColors.WindowText,Error=SystemColors.WindowText,Disabled=SystemColors.GrayText};
  bool dark=false;try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))dark=key!=null&&Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0;}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}
  return Create(dark);
 }
 public Color TextColor(object tag){if(!(tag is TextTone))return Text;switch((TextTone)tag){case TextTone.Muted:return Muted;case TextTone.Accent:return Accent;case TextTone.Warning:return Warning;case TextTone.Error:return Error;default:return Text;}}
}
static class WindowTheme {
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
 [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr handle,string subApp,string subId);
 public static void Caption(Form form,Palette theme){if(!form.IsHandleCreated)return;try{int dark=theme.Dark?1:0;if(DwmSetWindowAttribute(form.Handle,20,ref dark,4)!=0)DwmSetWindowAttribute(form.Handle,19,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
 public static void Input(Control control,Palette theme){if(!control.IsHandleCreated)return;try{SetWindowTheme(control.Handle,theme.Dark?"DarkMode_Explorer":"Explorer",null);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
}
}
