﻿using ChlaotModuleBase;
using CopilotModule;
using Eng.Chlaot.ChlaotModuleBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Eng.Chlaot.Modules.CopilotModule
{
  public class CopilotModule : NotifyPropertyChangedBase, IModule
  {
    public bool IsReady { get; private set; }

    private Control _InitControl;
    public Control InitControl => this._InitControl;

    private Control _RunControl;
    public Control RunControl => this._RunControl;

    public string Name => "Copilot";
    private LogHandler? parentLogHandler;
    private readonly LogHandler logHandler;
    private InitContext initContext;
    private RunContext runContext;
    private Settings settings;

    public CopilotModule()
    {
      this.IsReady = false;
      this.logHandler = (l, m) => parentLogHandler?.Invoke(l, m);
    }

    public void Init()
    {
      this.initContext = new InitContext(this.settings, logHandler, q => this.IsReady = q);
      this._InitControl = new CtrInit(this.initContext);
    }

    public void Run()
    {
      this.runContext = new RunContext();
      this._RunControl = new CtrRun(this.runContext);

      this.initContext = null;
      this._InitControl = null;
    }

    public void SetUp(ModuleSetUpInfo setUpInfo)
    {
      this.parentLogHandler = setUpInfo.LogHandler;
      this.parentLogHandler = setUpInfo.LogHandler;
      try
      {
        settings = Settings.Load();
        logHandler.Invoke(LogLevel.INFO, "Settings loaded.");
      }
      catch (Exception ex)
      {
        logHandler.Invoke(LogLevel.ERROR, "Unable to load settings. " + ex.GetFullMessage());
        logHandler.Invoke(LogLevel.INFO, "Default settings used.");
        settings = new Settings();
      }
    }

    public void Stop()
    {
      throw new NotImplementedException();
    }
  }
}