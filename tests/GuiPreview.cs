// Test fixture only: exercise the real UI with a palette override, without changing Windows settings.
using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Astra;
static class GuiPreview {
 [STAThread] static void Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  var form=new Gui();bool dark=args.Contains("dark")||Application.ExecutablePath.IndexOf("Dark",StringComparison.OrdinalIgnoreCase)>=0;string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-"+(dark?"dark":"light")+".log");
  form.Shown+=(s,e)=>{var timer=new Timer{Interval=750};timer.Tick+=(x,y)=>{timer.Stop();timer.Dispose();try{
   Require(form.Text=="Astra v1.1.0","release title");form.ApplyPalette(Palette.Create(dark));form.Text="UI review · "+(dark?"Dark":"Light");form.PerformLayout();
   Require(form.FormBorderStyle==FormBorderStyle.FixedSingle&&!form.MaximizeBox,"fixed window");
   var folder=Field<TextBox>(form,"folder");Require(folder.Text==Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"current-user desktop");
   var browse=Field<Button>(form,"browse");Require(browse.Height>=28&&browse.Width>=84&&browse.Parent.ClientRectangle.Contains(browse.Bounds),"folder button unclipped");
   foreach(var name in new[]{"speedValue","savedValue","elapsedValue"}){var label=Field<Label>(form,name);var card=label.Parent.Parent;Require(card is Surface,"metric surface");Require(card.Parent.ClientRectangle.Contains(card.Bounds),"metric outer bounds");Require(label.Parent.ClientRectangle.Contains(label.Bounds),"metric text bounds");Require(label.Parent.Bottom<card.Height-2,"metric bottom border reserved");}
   Require(form.BackColor==Palette.Create(dark).Window,"palette applies");
   form.ApplyPalette(Palette.Create(!dark));Require(form.BackColor==Palette.Create(!dark).Window,"live palette change");form.ApplyPalette(Palette.Create(dark));
   using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.ChangeExtension(report,"png"),System.Drawing.Imaging.ImageFormat.Png);}
   File.WriteAllText(report,"PASS fixed window, desktop folder, button bounds, all metric bounds, live palette switch\r\nClientSize="+form.ClientSize+"\r\n");
   if(args.Contains("--close"))form.Close();
  }catch(Exception ex){File.WriteAllText(report,ex.ToString());Environment.ExitCode=1;form.Close();}};timer.Start();};Application.Run(form);
 }
 static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static void Require(bool pass,string label){if(!pass)throw new Exception("UI regression: "+label);}
}
