// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Client.VisionScreensConfig
// Assembly: ClickerFixer.Client, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Client.dll

using YamlDotNet.Serialization;

#nullable disable
namespace ClickerFixer.Client
{
  internal class VisionScreensConfig
  {
    [YamlMember(Alias = "port", ApplyNamingConventions = false)]
    public int Port { get; set; } = 8979;
  }
}
