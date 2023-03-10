using CopilotModule;
using ELogging;
using Eng.Chlaot.ChlaotModuleBase;
using Eng.Chlaot.ChlaotModuleBase.ModuleUtils.StateChecking;
using Eng.Chlaot.ChlaotModuleBase.ModuleUtils.Synthetization;
using Eng.Chlaot.Modules.CopilotModule.Types;
using EXmlLib;
using EXmlLib.Deserializers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Xml.Linq;
using System.Xml.Serialization;
using static System.Net.Mime.MediaTypeNames;

namespace Eng.Chlaot.Modules.CopilotModule
{
  public class InitContext : NotifyPropertyChangedBase
  {
    private readonly NewLogHandler logHandler;
    private readonly Action<bool> setIsReadyFlagAction;

    public CopilotSet Set
    {
      get => base.GetProperty<CopilotSet>(nameof(Set))!;
      set => base.UpdateProperty(nameof(Set), value);
    }

    internal Settings Settings { get; private set; }

    internal InitContext(Settings settings, Action<bool> setIsReadyFlagAction)
    {
      Settings = settings ?? throw new ArgumentNullException(nameof(settings));
      this.logHandler = Logger.RegisterSender(this, "[Copilot.InitContext]");
      this.setIsReadyFlagAction = setIsReadyFlagAction ?? throw new ArgumentNullException(nameof(setIsReadyFlagAction));
    }

    internal void LoadFile(string xmlFile)
    {
      CopilotSet tmp;
      var factory = new XmlSerializerFactory();
      XDocument doc;

      try
      {
        logHandler.Invoke(LogLevel.INFO, $"Loading file '{xmlFile}'");
        try
        {
          doc = XDocument.Load(xmlFile);
          EXml<CopilotSet> exml = CreateDeserializer();
          tmp = exml.Deserialize(doc);
        }
        catch (Exception ex)
        {
          throw new ApplicationException("Unable to read/deserialize copilot-set from '{xmlFile}'. Invalid file content?", ex);
        }

        logHandler.Invoke(LogLevel.INFO, $"Checking sanity");
        try
        {
          CheckSanity(tmp);
        }
        catch (Exception ex)
        {
          throw new ApplicationException("Error loading checklist.", ex);
        }

        logHandler.Invoke(LogLevel.INFO, $"Analysing variables");
        try
        {
          AnalyseForUsedVariables(tmp);
        }
        catch (Exception ex)
        {
          throw new ApplicationException("Error analysing variables.", ex);
        }

        logHandler.Invoke(LogLevel.INFO, $"Loading/generating sounds");
        try
        {
          InitializeSoundStreams(tmp, System.IO.Path.GetDirectoryName(xmlFile)!);
        }
        catch (Exception ex)
        {
          throw new ApplicationException("Error creating sound streams.", ex);
        }

        this.Set = tmp;
        UpdateReadyFlag();
        logHandler.Invoke(LogLevel.INFO, $"Copilot set file '{xmlFile}' successfully loaded.");

      }
      catch (Exception ex)
      {
        this.setIsReadyFlagAction(false);
        logHandler.Invoke(LogLevel.ERROR, $"Failed to load copilot set from '{xmlFile}'." + ex.GetFullMessage());
      }
    }

    private void CheckSanity(CopilotSet tmp)
    {
      Stack<IStateCheckItem> stck = new();
      void checkStateCheckItem(IStateCheckItem sti)
      {
        stck.Push(sti);
        if (sti is StateCheckCondition stc)
          stc.Items.ForEach(q => checkStateCheckItem(q));
        else if (sti is StateCheckDelay std)
          checkStateCheckItem(std.Item);
        else if (sti is StateCheckProperty stp)
        {
          if (stp.Expression == null)
            throw new ApplicationException($"Expression of checked property {stp.DisplayName} not set." +
              $"Location: {string.Join(" ==> ", stck.Reverse().ToList().Select(q => q.DisplayString))}");
        }
        else if (sti is StateCheckTrueFalse sttf)
        {
          // intentionally blank
        }
        else
          throw new ApplicationException($"Unsupported type of '{nameof(IStateCheckItem)}'.");
        stck.Pop();
      }

      tmp.SpeechDefinitions.ForEach(q => checkStateCheckItem(q.When));
      tmp.SpeechDefinitions.ForEach(q => checkStateCheckItem(q.ReactivateWhen));
    }

    private void UpdateReadyFlag()
    {
      bool ready = this.Set != null && this.Set.SpeechDefinitions.SelectMany(q => q.Variables).All(q => q.HasValue);
      this.setIsReadyFlagAction(ready);
    }

    private void AnalyseForUsedVariables(CopilotSet set)
    {
      foreach (var sd in set.SpeechDefinitions)
      {
        sd.Variables.ForEach(q => q.Parent = sd);

        sd.Speech.GetUsedVariables()
          .Except(sd.Variables.Select(q => q.Name))
          .ToList()
          .ForEach(q => sd.Variables.Add(new Variable()
          {
            Name = q,
            DefaultValue = 0,
            Info = "(not provided)",
            Parent = sd
          }));

        ExtractVariablePairsFromStateChecks(sd)
          .Select(q => q.Item1)
          .Except(sd.Variables.Select(q => q.Name))
          .ToList()
          .ForEach(q => sd.Variables.Add(new Variable()
          {
            Name = q,
            DefaultValue = null,
            Info = "(not provided)",
            Parent = sd
          }));
        sd.Variables.ForEach(q => q.PropertyChanged += Variable_PropertyChanged);
        sd.Variables.ForEach(q => q.Value = q.DefaultValue);
      }
    }

    private void Variable_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
      Variable variable = (Variable)sender!;
      SpeechDefinition sd = variable.Parent
        ?? throw new ApplicationException($"Variable '{variable.Name}' not paired with its parent."); ;
      if (sd.Speech.Type == Speech.SpeechType.Speech
        && sd.Speech.GetUsedVariables().Any(q => q == variable.Name))
      {
        BuildSpeech(sd, new(), new Synthetizer(this.Settings.Synthetizer), "");
      }

      ExtractVariablePairsFromStateChecks(sd)
        .Where(q => q.Item1 == variable.Name)
        .ToList()
        .ForEach(q => q.Item2.Value = variable.Value);

      UpdateReadyFlag();
    }

    private static List<(string, StateCheckProperty)> ExtractVariablePairsFromStateChecks(SpeechDefinition sd)
    {
      List<(string, StateCheckProperty)> ret = new();
      Stack<IStateCheckItem> stack = new();

      stack.Push(sd.When);
      stack.Push(sd.ReactivateWhen);
      while (stack.Count > 0)
      {
        IStateCheckItem sci = stack.Pop();
        if (sci is StateCheckCondition scic)
          scic.Items.ForEach(q => stack.Push(q));
        else if (sci is StateCheckDelay scid)
          stack.Push(scid.Item);
        else if ((sci is StateCheckProperty scip) && scip.VariableName != null)
          ret.Add((scip.VariableName, scip));
      }
      return ret;
    }

    private static EXml<CopilotSet> CreateDeserializer()
    {
      EXml<CopilotSet> ret = new();

      var oed = new ObjectElementDeserializer()
        .WithCustomTargetType(typeof(CopilotSet))
        .WithCustomPropertyDeserialization(
        nameof(CopilotSet.SpeechDefinitions),
        (e, t, f, c) =>
        {
          var deser = c.ResolveElementDeserializer(typeof(SpeechDefinition));
          var items = e.LElements("speechDefinition")
            .Select(q => SafeUtils.Deserialize(q, typeof(SpeechDefinition), deser, c))
            .Cast<SpeechDefinition>()
            .ToList();
          SafeUtils.SetPropertyValue(f, t, items);
        });
      ret.Context.ElementDeserializers.Insert(0, oed);

      oed = new ObjectElementDeserializer()
        .WithCustomTargetType(typeof(SpeechDefinition))
        .WithCustomPropertyDeserialization(
          nameof(SpeechDefinition.Variables),
          (e, t, f, c) =>
          {
            var deser = c.ResolveElementDeserializer(typeof(Variable));
            var items = e.LElements("variable")
              .Select(q => SafeUtils.Deserialize(q, typeof(Variable), deser, c))
              .Cast<Variable>()
              .ToList();
            SafeUtils.SetPropertyValue(f, t, items);
          });
      ret.Context.ElementDeserializers.Insert(0, oed);

      oed = new ObjectElementDeserializer()
        .WithCustomTargetType(typeof(Speech))
        .WithIgnoredProperty(nameof(Speech.Bytes));
      ret.Context.ElementDeserializers.Insert(0, oed);

      ret.Context.ElementDeserializers.Insert(0, new StateCheckDeserializer());

      return ret;
    }

    private void InitializeSoundStreams(CopilotSet set, string relativePath)
    {
      Synthetizer synthetizer = new(Settings.Synthetizer);
      Dictionary<string, byte[]> generatedSounds = new();
      foreach (var sd in set.SpeechDefinitions)
      {
        BuildSpeech(sd, generatedSounds, synthetizer, relativePath);
      }
    }

    private void BuildSpeech(
      SpeechDefinition speechDefinition,
      Dictionary<string, byte[]> generatedSounds,
      Synthetizer synthetizer,
      string relativePath)
    {
      Speech speech = speechDefinition.Speech;
      if (speech.Type == Speech.SpeechType.File)
        try
        {
          speech.Bytes = System.IO.File.ReadAllBytes(
            System.IO.Path.Combine(relativePath, speech.Value));
        }
        catch (Exception ex)
        {
          throw new EXmlException($"Unable to load sound file '{speech.Value}'.", ex);
        }
      else if (speech.Type == Speech.SpeechType.Speech)
      {
        string txt = speech.GetEvaluatedValue(speechDefinition.Variables);
        try
        {
          if (generatedSounds.ContainsKey(txt))
            speech.Bytes = generatedSounds[txt];
          else
          {
            speech.Bytes = synthetizer.Generate(txt);
            generatedSounds[txt] = speech.Bytes;
          }
        }
        catch (Exception ex)
        {
          throw new EXmlException($"Unable to generated sound for speech '{speech.Value}'.", ex);
        }
      }
      else
        throw new NotImplementedException($"Unknown type {speech.Type}.");
    }
  }
}
