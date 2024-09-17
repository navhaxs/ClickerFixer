// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Client.Config
// Assembly: ClickerFixer.Client, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Client.dll

using YamlDotNet.Serialization;

#nullable enable
namespace ClickerFixer.Client
{
  internal class Config
  {
    [YamlMember(Alias = "server", ApplyNamingConventions = false)]
    public ServerConfig ServerConfig { get; set; } = new ServerConfig();

    [YamlMember(Alias = "targets", ApplyNamingConventions = false)]
    public TargetsConfig TargetsConfig { get; set; } = new TargetsConfig();
  }
}
