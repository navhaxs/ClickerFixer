// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Client.TargetsConfig
// Assembly: ClickerFixer.Client, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Client.dll

using YamlDotNet.Serialization;

#nullable enable
namespace ClickerFixer.Client
{
  internal class TargetsConfig
  {
    [YamlMember(Alias = "visionscreens", ApplyNamingConventions = false)]
    public VisionScreensConfig VisionScreensConfig { get; set; } = new VisionScreensConfig();

    [YamlMember(Alias = "propresenter", ApplyNamingConventions = false)]
    public ProPresenterConfig ProPresenterConfig { get; set; } = new ProPresenterConfig();
  }
}
