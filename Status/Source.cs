using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Tildetool.Status
{
   public abstract class Source
   {
      public enum StateType
      {
         Inactive,
         Error,
         Alert,
         Success
      }
      static Dictionary<StateType, Color[]> sStateColor = new Dictionary<StateType, Color[]>
      {
         { StateType.Inactive, new Color[2] { Color.FromArgb(0xFF, 0xAB, 0xAB, 0xAB), Color.FromArgb(0xFF, 0x5F, 0x5F, 0x5F) } },
         { StateType.Error,    new Color[2] { Color.FromArgb(0xFF, 0xDE, 0x5D, 0x5D), Color.FromArgb(0xFF, 0x84, 0x2F, 0x2F) } },
         { StateType.Alert,    new Color[2] { Color.FromArgb(0xFF, 0xEC, 0xD6, 0x43), Color.FromArgb(0xFF, 0x92, 0x84, 0x26) } },
         { StateType.Success,  new Color[2] { Color.FromArgb(0xFF, 0x57, 0xdb, 0x42), Color.FromArgb(0xFF, 0x2D, 0x77, 0x22) } },
      };

      public string Title { get; private set; }
      public string Subtitle { get; private set; }
      public string Guid { get; private set; }

      public int ChangeIndex { get; private set; }
      private string _Status;
      private StateType _State;
      public string Status { get { return _Status; } set { if (_Status != value) ChangeIndex++; _Status = value; } }
      public StateType State { get { return _State; } set { if (_State != value) ChangeIndex++; _State = value; } }

      public Color Color { get { return sStateColor[_State][0]; } }
      public Color ColorDim { get { return sStateColor[_State][1]; } }

      public Source(string title, string subtitle)
      {
         Title = title;
         Subtitle = subtitle;

         byte[] guidsource = ASCIIEncoding.ASCII.GetBytes(title + "___" + subtitle);
         byte[] guidarray = new MD5CryptoServiceProvider().ComputeHash(guidsource);
         Guid = Convert.ToHexString(guidarray);

         Status = "...working...";
         State = StateType.Inactive;
      }
      public void Initialize(string status, StateType state)
      {
         Status = status;
         State = state;
      }

      public Task RefreshTask { get; protected set; }
      public Task Refresh()
      {
         RefreshTask = Task.Run(() =>
         {
            try
            {
               _Refresh();
            }
            catch (Exception ex)
            {
               Console.WriteLine(ex.ToString());
               Status = "intern err";
               State = StateType.Error;
            }
         });
         return RefreshTask;
      }
      protected abstract void _Refresh();

      public abstract bool Ephemeral { get; }
      public abstract bool NeedsRefresh(TimeSpan interval);
   }
}