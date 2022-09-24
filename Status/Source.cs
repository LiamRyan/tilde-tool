using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Text.Json;

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
         { StateType.Inactive, new Color[3] { Extension.FromArgb(0xFF212121), Extension.FromArgb(0xFF8C8C8C), Extension.FromArgb(0xFF8C8C8C) } },
         { StateType.Error,    new Color[3] { Extension.FromArgb(0xFF271515), Extension.FromArgb(0xFF963737), Extension.FromArgb(0xFF842F2F) } },
         { StateType.Alert,    new Color[3] { Extension.FromArgb(0xFF21230C), Extension.FromArgb(0xFFEAF1AF), Extension.FromArgb(0xFF969237) } },
         { StateType.Success,  new Color[3] { Extension.FromArgb(0xFF042508), Extension.FromArgb(0xFFC3F1AF), Extension.FromArgb(0xFF449637) } },
      };

      public string Title { get; private set; }
      public string Subtitle { get; private set; }
      public string Guid { get; private set; }

      public int ChangeIndex { get; private set; }
      private string _Status;
      private string _Article;
      private StateType _State;
      public string Status { get { return _Status; } set { if (_Status != value) ChangeIndex++; _Status = value; } }
      public string Article { get { return _Article; } set { if (_Article != value) ChangeIndex++; _Article = value; } }
      public StateType State { get { return _State; } set { if (_State != value) ChangeIndex++; _State = value; } }


      private Type _CacheType;
      private object? _Cache;
      public object? Cache { get { return _Cache; } set { if (_Cache != null && value != null && !_Cache.Equals(value)) ChangeIndex++; if ((_Cache == null) != (value == null)) ChangeIndex++; _Cache = value; } }

      public Color ColorBack { get { return sStateColor[_State][0]; } }
      public Color Color { get { return sStateColor[_State][1]; } }
      public Color ColorDim { get { return sStateColor[_State][2]; } }

      public Source(string title, string subtitle, Type cacheType)
      {
         Title = title;
         Subtitle = subtitle;

         byte[] guidsource = ASCIIEncoding.ASCII.GetBytes(title + "___" + subtitle);
         byte[] guidarray = new MD5CryptoServiceProvider().ComputeHash(guidsource);
         Guid = Convert.ToHexString(guidarray);

         Status = "...working...";
         Article = "";
         State = StateType.Inactive;
         _CacheType = cacheType;
         Cache = null;
      }
      public void Initialize(string status, string article, StateType state, string cache)
      {
         Status = status;
         Article = article;
         State = state;
         Cache = null;
         try
         {
            if (!string.IsNullOrEmpty(cache))
               Cache = JsonSerializer.Deserialize(cache, _CacheType);
         }
         catch (Exception e)
         {
            App.WriteLog(e.ToString());
         }
      }

      public string GetCache()
      {
         if (Cache == null)
            return "";
         return JsonSerializer.Serialize(Cache, _CacheType);
      }

      public bool IsQuerying { get { return QueryTask != null && !QueryTask.IsCompleted; } }
      public Task QueryTask { get; protected set; }
      public Task Query()
      {
         QueryTask = Task.Run(() =>
         {
            try
            {
               _Query();
            }
            catch (Exception ex)
            {
               App.WriteLog(ex.ToString());
               Status = "intern err";
               State = StateType.Error;
            }
         });
         return QueryTask;
      }
      protected abstract void _Query();
      public abstract void Display();

      public abstract bool IsFeed { get; }
      public abstract bool Ephemeral { get; }
      public abstract bool Important { get; }
      public abstract int Order { get; }
      public abstract string Domain { get; }
      public abstract bool NeedsRefresh(TimeSpan interval);
      public abstract void HandleClick();
   }
}