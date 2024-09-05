/*
 * Invented by Frank Scott. Written by S. Rives and F. Scott
 * User: GTP Innovate 2024
 * Date: 8/24/2024
 * Time: 3:00 PM
 *
 *        A macro to publish your model to Stratus on a schedule, will Sync and Save before publish
 * 
 */
using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;            // In SharpDevelop, click on "References" and add this DLL. Timer code lives in here
using System.Runtime.InteropServices;  // We need this to keep our computer from going into sleep mode 

namespace Utilities
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.DB.Macros.AddInId("0962CE0E-1BAE-4461-8737-E4F3F4451ED5")]
    public partial class ThisApplication
    {
      [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] // Turn on/off Windows Sleep
      static extern uint SetThreadExecutionState(uint esFlags); 
	    
      private int _hourToPublish = 22; // 22 = 10pm military time
      private Timer _scheduler;   
      private void Module_Startup(object sender, EventArgs e)
      {		
         _scheduler = new Timer();
         _scheduler.Interval = 1000; // 1000 = 1 second. Eevery second, check to see if it is time to publish.       
         _scheduler.Tick += SchedulerCallback;
      }
		  
      private void Module_Shutdown(object sender, EventArgs e)
      {			
      }
		
      #region Revit Macros generated code
      private void InternalStartup()
      {
         this.Startup += new System.EventHandler(Module_Startup);
         this.Shutdown += new System.EventHandler(Module_Shutdown);
      }
      #endregion

      private bool _forceCheckoutWorksets = false;
      string _settingsFileName = Environment.GetEnvironmentVariable("APPDATA") + "\\GTP Software Inc\\STRATUS\\settings.json";

      /// <summary>
      /// Check settings.json to see if Import.ForceCheckoutWorksets is true.
      /// If enableSetting == true, then update settings.json set Import.ForceCheckoutWorksets=true;
      /// Only works if settings.json is pretty printed and there is an existing Import section
      /// </summary>
      private bool ForceCheckoutWorksets(bool enableSetting=false)
      {        
         var forceCheckoutWorksets = -1; // -1 means we did not find ForceCheckoutWorksets in Settings.xml
         var insertHere = -1; // -1 means we did not find an Import section in Settings.xml
         var theSetting = Environment.NewLine + "    \"ForceCheckoutWorksets\": " + (enableSetting ? "true," : "false,");
         try
         {
           var lines = System.IO.File.ReadAllLines(_settingsFileName);
           for (var i = 0; i < lines.Length; i++)
           {
              if (lines[i].Contains("\"Import\"") && !lines[i].Contains("}"))
              {
                 insertHere = i;
                 if (!lines[i].Contains("{")) insertHere++;                 
              }
              if (lines[i].Contains("ForceCheckoutWorksets"))
              {
                 forceCheckoutWorksets = i;
              	 if (!lines[i].Contains("}"))
              	 {
                   if (lines[i].Contains("true")) return true;
                   if (!enableSetting) return false; // they just want to know the current value
                   if (enableSetting) lines[i]=lines[i].Replace("false", "true");
              	}
              }
           }
           if (forceCheckoutWorksets == -1 && insertHere > -1)
           {
              lines[insertHere] += theSetting;
           }
           if (enableSetting)
           {
              System.IO.File.WriteAllLines(_settingsFileName, lines);
              return true;
           }
         }
         catch
         {
         	MessageBox.Show("Manually update the file: " + Environment.NewLine + _settingsFileName + Environment.NewLine +
                                "and add this to the Import section: " + theSetting + Environment.NewLine +
                                "then restart Revit.", "Import: { ForceCheckoutWorksets }");
         }
         return false;
      }
	    
      /// <summary>
      /// -------------------------------------------------------------
      /// MACRO ENTRY POINT -- search logs for last publish
      /// -------------------------------------------------------------
      /// </summary>
      public void ShowLastSuccessfulSilentPublish()
      {
      	for (int i = 0; i < 20; i++)
        {
           if (CheckLogsForSilentPublish(DateTime.MinValue, true, i, false))
           {
              break;
           }     		
     	}
      }
      
      /// <summary>
      /// -------------------------------------------------------------
      /// MACRO ENTRY POINT -- show all the custom data in all your parts
      /// -------------------------------------------------------------	    
      /// </summary>
      public void ShowAllPartCustomData()
      {
        Document doc = ActiveUIDocument.Document;
        var conf = FabricationConfiguration.GetFabricationConfiguration(doc);
        var confCD = conf.GetAllPartCustomData();
        string CDList = "";
        foreach(int cdint in confCD)            
        {
           CDList = CDList +"ID "+ cdint.ToString() +": "+ conf.GetPartCustomDataName(cdint).ToString()+"\n";
        }
        TaskDialog.Show("Custom Data", CDList);
     }

     // We return true if we updated Settings.Json and a restart of Revit is needed
     private bool AskForceCheckoutWorksets()
     {
         var restartRevit = false;
         if (!_forceCheckoutWorksets) // if the settings.Json file does NOT have ForceCheckoutWorksets = true, ask them about it
         {
            if (ForceCheckoutWorksets())
            {
               restartRevit = true;
            }
            else
            {
                var setting = TaskDialog.Show("Change Settings.json?", "The silent publish may fail by presenting you" + Environment.NewLine +
						  "with dialog questions for which you won't be around to answer." + Environment.NewLine + Environment.NewLine +
						  "To disable the Workset Checkout dialog, modify the following Stratus settings file:" + Environment.NewLine +
						   Environment.NewLine +_settingsFileName + Environment.NewLine + Environment.NewLine +
						   "and add this setting:" + Environment.NewLine +
						   "     \"ForceCheckoutWorksets\": true " + Environment.NewLine +
						   "to the \"Import\": section" + Environment.NewLine+
						   Environment.NewLine + Environment.NewLine +
						   "Would you like this macro to modify your Settings.json file for you?",
						   TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
	
                if (setting == TaskDialogResult.Yes && ForceCheckoutWorksets(true))
                {
                  restartRevit = true;
                }
             }
         }
         if (restartRevit)
         {
             MessageBox.Show("Updated " + Environment.NewLine + _settingsFileName + Environment.NewLine +
                            "You must restart Revit to continue.", "Restart Revit.");        
         }
         return restartRevit;
      }
	    
      /// <summary>
      /// -------------------------------------------------------------
      /// MACRO ENTRY POINT -- Revit calls into here
      /// -------------------------------------------------------------
      /// </summary>
      public void PublishToStratusTonight()
      {			      
         // If we are already running a scheduler, then ask them if they want to cancel it.
         if (_scheduler.Enabled)
         {
            var select = TaskDialog.Show("Already Scheduled to Publish", "Would you like to cancel the publish scheduled for military hour " + _hourToPublish + "?", 
		                                 TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            if (select == TaskDialogResult.Yes)
            {
               SetThreadExecutionState(1); // Turn off Stay Awake
               _scheduler.Stop();
            }
            return;
         }
	      
         if (AskForceCheckoutWorksets())
         {
            return;
         }
	      
         // Ask them if they really want to schedule a silent publish (show them YES and NO buttons)
         var doIt = TaskDialog.Show("Continue?", 
                                    "Before you run this Macro:\r\n" +
                                    " 1. Log into gtpstratus.com\r\n" +
                                    " 2. Switch to the company you want to publish to\r\n" +
                                    "      (relevant if you have a Sandbox company)\r\n" +
                                    " 3. In Revit, go to the Add-Ins ribbon tab, then click\r\n" +
                                    "      Help (drop down) -> Sign Out\r\n" +
                                    " 4. In Revit, go to the Add-Ins ribbon tab, then click\r\n" +
                                    "      External Tools->Stratus Set Project Info\r\n" +
                                    " 5. Login, then cancel out of the following dialog.\r\n\r\n" +
                                    "Would you like to schedule a silent publish for military hour " + _hourToPublish + "?",
                                     TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

         if (doIt == TaskDialogResult.Yes)
         {
             SetThreadExecutionState(0x80000043); // 80,000,043 --> Tell Windows to Stay Awake 
             _scheduler.Start();                  // Start the scheduler, which will check to see when it is the _hourToPublish (see SchedulerCallback)
         }
     }

     /// -------------------------------------------------------------
     /// If you publish late at night, first make sure nobody has changes
     /// you haven't merged into your model
     /// -------------------------------------------------------------
     private void SyncAndSave()
     {
       try
       {
         Document doc = this.ActiveUIDocument.Document;
         if (doc.IsWorkshared)
         {
            SynchronizeWithCentralOptions syncOptions = new SynchronizeWithCentralOptions();
            RelinquishOptions relinquishOptions = new RelinquishOptions(true); // relinquish everything
            TransactWithCentralOptions transactOptions = new TransactWithCentralOptions();
            syncOptions.SetRelinquishOptions(relinquishOptions);
            doc.SynchronizeWithCentral(transactOptions, syncOptions);
            doc.Save();
         }
       }
       catch (Exception ex)
       {
          TaskDialog.Show("Error", "An error occurred: " + ex.Message);
       }
     } 
	    
     /// -------------------------------------------------------------
     /// This is the Timer function that gets called once ever so often
     /// and checks to see if we have hit the desired hour of the day
     /// to Silent Publish (set _hourToPublish to control schedule)
     /// -------------------------------------------------------------
     private void SchedulerCallback(object sender, EventArgs e)
     {			
        if (_scheduler.Enabled && DateTime.Now.Hour == _hourToPublish) // 22 = 10pm military time
        {
          _scheduler.Stop();
          SyncAndSave();
          RunSilentPublishNow();
          // CheckLogsForSilentPublish(pubTime);
          SetThreadExecutionState(1); // no longer try to keep the computer from sleeping
        }
     }

     // From a log-line, get the DateTime stamp on it
     private DateTime GetDateTime(string line, DateTime def)
     {
     	if (string.IsNullOrEmpty(line))
     	{
           return def;
     	}
     	var parts = line.Split('[');
     	if (parts.Length > 0)
     	{
           DateTime res;
           if (DateTime.TryParse(parts[0], out res))
           {
             return res;
           }
     	}
     	return def;
     }
	    
     // -------------------------------------------------------------
     // Walk through the latest log for the current model
     // -------------------------------------------------------------
     private bool CheckLogsForSilentPublish(DateTime pubTime, bool verbose=false, int ct=0, bool showCrashError=true)
     {
       var msgBoxShown = false;
       try
       {
         var silent = false;
     	 var errorMsg = string.Empty;
     	 var silentLine = string.Empty;
     	 
         Document doc = this.ActiveUIDocument.Document;
         var name = doc.Title;
         
         var log = Environment.GetEnvironmentVariable("APPDATA") + "\\GTP Software Inc\\STRATUS Logs\\Revit\\Log - " + name + " - Extract.txt";
         if (ct > 0)
         {
           log += "." + ct;
         }
         if (System.IO.File.Exists(log))
         {
            var lines = System.IO.File.ReadAllLines(log);
            if (lines != null && lines.Length > 0)
            {
               var rev = lines.Reverse();
               foreach(var line in rev)
               {
               	  var dt = GetDateTime(line, pubTime);
               	  if (dt < pubTime)
               	  {
               	     break;
               	  }
                  if (line.Contains("IN SILENT MODE"))
                  {
                     silentLine = line;
                     silent = true;
               	     break;
                  }
                  if (line.Contains("ERROR"))
                  {
                     errorMsg += line + "\r\n";
                  }
               }
            }
         }
         if (silent == true && !string.IsNullOrEmpty(errorMsg))
         {
            msgBoxShown = true;
            errorMsg += "\r\n" + log;
         	MessageBox.Show(errorMsg, "Email this: " + name);
         }
         else if (silent == true && verbose)
         {
            msgBoxShown = true;
            silentLine += "\r\n\r\n" + log;
            MessageBox.Show(silentLine, "Silent Publish Success: " + name);
         }
       }
       catch (Exception ex)
       {
       	 if (showCrashError)
       	 {
       	   MessageBox.Show(ex.Message, "Failed");
       	 }
       }
       return msgBoxShown;
     }
	    
     /// -------------------------------------------------------------
     /// This is the function that does the actual Silent Publish 
     /// command. This will get called by the scheduler at the right
     /// hour of the day
     /// -------------------------------------------------------------
     private void RunSilentPublishNow()
     {
        Document doc = this.ActiveUIDocument.Document; 
        var app = doc.Application;
        UIApplication uiapp = new UIApplication(app);
       
        try
        {
           //Look in C:\ProgramData\Autodesk\ApplicationPlugins\Gtpx.ModelSync.Addin.Revit2021.bundle\Contents\Gtpx.ModelSync.Addin.Revit2021.addin					
           var addinId = Autodesk.Revit.UI.RevitCommandId.LookupCommandId("a584d14f-3ba0-4f78-8719-7640168260ec");
           uiapp.PostCommand(addinId); // check your log file when done. This can fail if you are not logged into your Stratus company
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Publish to Stratus Tonight", "ERROR: Could not launch silent publish: " + ex.Message);
        }
     }
   }
}
