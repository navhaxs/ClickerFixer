// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Desktop.Config
// Assembly: ClickerFixer.Desktop, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Desktop.dll

using System.Collections.Generic;
using YamlDotNet.Serialization;

#nullable enable
namespace ClickerFixer.Desktop
{
  internal class Config
  {
    [YamlMember(Alias = "server", ApplyNamingConventions = false)]
    public ServerConfig ServerConfig { get; set; } = new ServerConfig();

    [YamlMember(Alias = "targets", ApplyNamingConventions = false)]
    public TargetsConfig TargetsConfig { get; set; } = new TargetsConfig();

    // Order clicker targets are tried in - first IsActive() match wins. Names must match
    // the target class names (ProPresenter, VisionScreens, PowerPoint, Native).
    [YamlMember(Alias = "target-priority", ApplyNamingConventions = false)]
    public List<string> TargetPriority { get; set; } = new List<string>
    {
      "ProPresenter", "VisionScreens", "PowerPoint", "Native"
    };
  }
}
