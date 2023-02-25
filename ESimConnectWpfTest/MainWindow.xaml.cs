﻿using ESimConnect;
using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ESimConnectWpfTest
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct OtherDataStruct
    {
      [DataDefinition("CAMERA STATE")]
      public int cameraState;

      [DataDefinition(SimVars.Aircraft.Miscelaneous.TITLE)]
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
      public string title;

      [DataDefinition(SimVars.Aircraft.Miscelaneous.AIRSPEED_INDICATED, SimUnits.Speed.KNOT)]
      public int speed;

      [DataDefinition(SimVars.Aircraft.Miscelaneous.PLANE_ALTITUDE, SimUnits.Length.FOOT)]
      public int altitude;

      [DataDefinition(SimVars.Aircraft.Miscelaneous.PLANE_ALTITUDE, SimUnits.Length.FOOT)]
      public double altitude2;

      [DataDefinition(SimVars.Aircraft.Miscelaneous.PLANE_ALT_ABOVE_GROUND, SimUnits.Length.FOOT)]
      public int height;

      [DataDefinition(SimVars.Aircraft.Miscelaneous.PLANE_ALT_ABOVE_GROUND, SimUnits.Length.FOOT)]
      public double height2;

      [DataDefinition(SimVars.Aircraft.Miscelaneous.PLANE_ALT_ABOVE_GROUND, SimUnits.Length.METER, SimType.INT32)]
      public int height3;

      [DataDefinition(SimVars.Aircraft.Miscelaneous.PLANE_BANK_DEGREES, SimUnits.Angle.DEGREE, SimType.INT32)]
      public int bank;

      [DataDefinition(SimVars.Aircraft.BrakesAndLandingGear.BRAKE_PARKING_POSITION, type: SimType.INT32)]
      public int parkBrake;

      [DataDefinition(SimVars.Miscellaneous.SIM_DISABLED, type: SimType.INT32)]
      public int simDisabled;
    };

    private ESimConnect.ESimConnect simCon;

    public MainWindow()
    {
      InitializeComponent();
    }

    private BindingList<PropertyInfo> properties = new();
    private void Window_Initialized(object sender, EventArgs e)
    {
      this.simCon = new();

      lstProperties.ItemsSource = properties;
    }

    private void btnUpdate_Click(object sender, RoutedEventArgs e)
    {
      bool isOpened;

      try
      {
        isOpened = simCon.IsOpened;
      }
      catch (Exception ex)
      {
        Log("Failed to access sim-con.", ex);
        return;
      }

      if (isOpened)
        Update();
      else
        Connect();

      if (simCon.IsOpened)
        btnUpdate.Content = "Update";
    }

    private void Update()
    {
      Log("Requesting Update");
      //this.simCon.RequestData<OtherDataStruct>();
      //this.simCon.RequestDataRepeatedly<OtherDataStruct>(null, SIMCONNECT_PERIOD.SECOND);
      this.simCon.RegisterEvent(11, SimEvents._4sec);
      Log("Requested Update");
    }

    private void Connect()
    {
      Log("Connect");
      try
      {
        simCon.Open();
        Log("Connected");
      }
      catch (Exception ex)
      {
        Log("Failed to connect.", ex);
        return;
      }

      Log("Registering events");
      try
      {
        simCon.Disconnected += SimCon_Disconnected;
        simCon.Connected += SimCon_Connected;
        simCon.DataReceived += SimCon_DataReceived;
        simCon.ThrowsException += SimCon_ThrowsException;
        simCon.EventInvoked += SimCon_EventInvoked;
        Log("Events registered");
      }
      catch (Exception ex)
      {
        Log("Failed to register events.", ex);
        return;
      }

      Log("Registering type");
      try
      {
        simCon.RegisterType<OtherDataStruct>(1);
        Log("Type registered.");
      }
      catch (Exception ex)
      {
        Log("Failed to register the type.", ex);
        return;
      }
    }

    private void SimCon_EventInvoked(ESimConnect.ESimConnect sender, ESimConnect.ESimConnect.ESimConnectEventInvokedEventArgs e)
    {
      Log("SimConnect-internal Event raised with request " + e.RequestId + " and value " + e.Value);
    }

    private void SimCon_ThrowsException(ESimConnect.ESimConnect sender, SIMCONNECT_EXCEPTION ex)
    {
      Log("SimConnect-internal throws an exception: " + ex.ToString());
    }

    private void SimCon_DataReceived(ESimConnect.ESimConnect sender, ESimConnect.ESimConnect.ESimConnectDataReceivedEventArgs e)
    {
      Log("SimConnect-internal got data.");

      //DataStruct ds = default;
      //try
      //{
      //  ds = (DataStruct)e.Data;
      //}
      //catch (Exception ex)
      //{
      //  Log("Failed to convert obtained data to my structure.", ex);
      //}

      //UpdateDataView(ds);
      UpdateDataView(e.Data);
    }

    private void UpdateDataView(object ds)
    {
      var fields = ds.GetType().GetFields(
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

      properties.Clear();

      properties.Add(new PropertyInfo() { Name = "Date&Time", Value = DateTime.Now.ToString() });

      foreach (var field in fields)
      {
        PropertyInfo pi = new PropertyInfo()
        {
          Name = field.Name,
          Value = field.GetValue(ds)
        };
        properties.Add(pi);
      }
      Log("Properties view refreshed.");
    }

    private void SimCon_Connected(ESimConnect.ESimConnect sender)
    {
      Log("SimConnect-internal connected.");
    }

    private void SimCon_Disconnected(ESimConnect.ESimConnect sender)
    {
      Log("SimConnect-internal disconnected.");
    }

    private void Log(string text, Exception? ex)
    {
      List<string> tmp = new();
      while (ex != null)
      {
        tmp.Add(ex.Message + " (" + ex.StackTrace + ")");
        ex = ex.InnerException;
      }
      Log(text + "\t\n" + string.Join("\n\t", tmp));
    }

    private void Log(string text)
    {
      txtLog.AppendText("\n" + text);
      txtLog.ScrollToEnd();
    }
  }
}
