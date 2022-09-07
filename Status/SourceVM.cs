using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Tildetool.Status
{
   internal class SourceVM : Source
   {
      protected record CacheStruct : IEquatable<CacheStruct>
      {
         public string Status { get; set; }
      }

      protected string VmBoxPath;
      protected string VmName;
      protected string VmIp;
      public SourceVM(string name, string vboxPath, string vmname, string ip)
         : base("VM", name, typeof(CacheStruct))
      {
         VmBoxPath = vboxPath;
         VmName = vmname;
         VmIp = ip;
      }

      protected override void _Query()
      {
         // Check the VM status from Oracle.
         {
            // Do a query
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = VmBoxPath;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.ArgumentList.Add("showvminfo");
            startInfo.ArgumentList.Add(VmName);
            startInfo.ArgumentList.Add("--machinereadable");
            process.StartInfo = startInfo;
            process.Start();

            // Read the status output.
            string status = "unknown";
            Task<string?> task = process.StandardOutput.ReadLineAsync();
            task.Wait();
            string? line = task.Result;
            while (line != null)
            {
               if (line.StartsWith("VMState"))
               {
                  status = line.Split('=', 2)[1].Trim('"');
                  break;
               }
               task = process.StandardOutput.ReadLineAsync();
               task.Wait();
               line = task.Result;
            }

            // If it's not running, show that.
            if (status != "running")
            {
               Cache = new CacheStruct { Status = status };
               return;
            }
         }

         // Ping it
         bool pingResult = false;
         using (Ping pinger = new Ping())
         {
            PingReply reply = pinger.Send(VmIp);
            pingResult = reply.Status == IPStatus.Success;
         }

         // If it didn't respond .
         if (!pingResult)
         {
            Cache = new CacheStruct { Status = "no ping" };
            return;
         }

         /*
         // Do an SSH query.
         bool sshResult = false;
         using (var client = new SshClient(VmIp, VmUser))
         {
            client.Connect();
            sshResult = client.IsConnected;
            client.Disconnect();
         }

         // If it didn't respond .
         if (!sshResult)
         {
            Status = "no ssh";
            State = StateType.Alert;
            return;
         }
         */

         // All good, return the result!
         Cache = new CacheStruct { Status = "online" };
      }
      public override void Display()
      {
         CacheStruct cache = Cache as CacheStruct;
         if (cache == null)
            return;
         Status = "";
         Article = cache.Status;

         if (cache.Status == "online")
            State = StateType.Success;
         else if (cache.Status == "no ping")
            State = StateType.Alert;
         else if (cache.Status == "poweroff")
            State = StateType.Inactive;
         else
            State = StateType.Alert;
      }

      public override bool Ephemeral { get { return true; } }
      public override bool Important { get { return false; } }
      public override string Domain { get { return VmIp; } }
      public override bool NeedsRefresh(TimeSpan interval)
      {
         if (State == StateType.Inactive)
            return interval.TotalSeconds >= 1.0f;
         return interval.TotalSeconds >= 5.0f;
      }
      public override void HandleClick()
      {
      }
   }
}
