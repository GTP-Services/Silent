/*
 * Invented by Frank Scott. Written by S. Rives and F. Scott
 * User: GTP Innovate 2024
 * Date: 8/24/2024
 * Time: 3:00 PM
 *
 *        A macro to publish your model to Stratus on a schedule, will Sync and Save before publish
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

      [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] // Turn on/off Windows Sleep
      static extern uint SetThreadExecutionState(uint esFlags);            
		
      /// <summary>
      /// MACRO ENTRY POINT -- Revit calls into here
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

         // Ask them if they really want to schedule a silent publish (show them YES and NO buttons)
         var doIt = TaskDialog.Show("Continue?", 
				    "Before you run this Macro, you must LOG IN to Stratus and Revit-Stratus (both places).\r\n" +
                                    "    1. You must log into gtpstratus.com, and switch to the company you want to publish to\r\n" +
                                    "    2. In Revit, go to Add Ins, Help, Sign-out\r\n" +
                                    "    3. In Revit, go to Add Ins, External Tools, Stratus Set Project Info (this will prompt the login, after that just cancel out of the dialog)\r\n\r\n" +
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
         SynchronizeWithCentralOptions syncOptions = new SynchronizeWithCentralOptions();
         RelinquishOptions relinquishOptions = new RelinquishOptions(true);
         relinquishOptions.StandardWorksets = true;
         relinquishOptions.ViewWorksets = true;
         relinquishOptions.FamilyWorksets = true;
         relinquishOptions.UserWorksets = true;
         relinquishOptions.CheckedOutElements = true;

         TransactWithCentralOptions transactOptions = new TransactWithCentralOptions();
         syncOptions.SetRelinquishOptions(relinquishOptions);

         doc.SynchronizeWithCentral(transactOptions, syncOptions);
         doc.Save();
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
          SetThreadExecutionState(1); // no longer try to keep the computer from sleeping
        }
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
