﻿using Eng.Chlaot.Modules.AffinityModule;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace AffinityModule
{
  /// <summary>
  /// Interaction logic for CtrRun.xaml
  /// </summary>
  public partial class CtrRun : UserControl
  {
    private readonly Context context;
    public CtrRun()
    {
      InitializeComponent();
      this.context = null!;
    }

    public CtrRun(Context context) : this()
    {
      this.context = context;
      this.DataContext = context;
    }
  }
}