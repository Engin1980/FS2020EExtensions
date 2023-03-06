﻿using ChlaotModuleBase;
using ELogging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Eng.Chlaot.Modules.AffinityModule
{
  public class Rule
  {
    public const string ROLL_REGEX = "^((\\d+)(-(\\d+))?)(,(\\d+)(-(\\d+))?)*$";
    private static readonly int numberOfCores = Environment.ProcessorCount;

    public List<bool> CoreFlags { get; set; }

    public string TitleOrRegex => this.Title ?? this.Regex ?? "(null)";

    public string? Title { get; set; }

    private string _Roll;
    public string Roll
    {
      get => this._Roll;
      set
      {
        this._Roll = value;
        ExpandToCores();
      }
    }

    public string Regex { get; set; }

    public Rule()
    {
      this.CoreFlags = BuildCoresList();
      _Roll = Roll = "";
      Regex = ".+";
    }

    private static List<bool> BuildCoresList()
    {
      var ret = new List<bool>();
      for (int i = 0; i < numberOfCores; i++)
        ret.Add(false);
      return ret;
    }

    private void ExpandToCores()
    {
      List<int> includedIndices = new();

      if (Roll.Length > 0)
      {
        if (System.Text.RegularExpressions.Regex.IsMatch(Roll, ROLL_REGEX) == false)
        {
          Logger.Log(this, LogLevel.WARNING, $"CoresPatter '{Roll}' is not in valid format.");
          return;
        }

        string[] pts = this.Roll.Split(';');
        foreach (string pt in pts)
        {
          if (pt.Contains('-'))
          {
            string[] tms = pt.Split('-');
            int fromIndex = int.Parse(tms[0]);
            int toIndex = int.Parse(tms[1]);
            for (int i = fromIndex; i <= toIndex; i++)
              includedIndices.Add(i);

          }
          else
          {
            int index = int.Parse(pt);
            includedIndices.Add(index);
          }
        }

        for (int i = 0; i < CoreFlags.Count; i++)
        {
          CoreFlags[i] = includedIndices.Contains(i);
        }
      }
    }

    internal IntPtr CalculateAffinity()
    {
      BitArray bitArray = new(CoreFlags.ToArray());
      int intLen = (int)Math.Ceiling(numberOfCores / 8d);
      int[] tmp = new int[intLen];
      bitArray.CopyTo(tmp, 0);
      IntPtr ret = (IntPtr)tmp[0];
      return ret;
    }
  }
}
