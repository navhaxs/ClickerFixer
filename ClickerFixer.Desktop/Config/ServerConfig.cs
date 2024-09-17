// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Client.ServerConfig
// Assembly: ClickerFixer.Client, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Client.dll

using YamlDotNet.Serialization;

#nullable enable
namespace ClickerFixer.Client
{
  internal class ServerConfig
  {
    [YamlMember(Alias = "ip-address", ApplyNamingConventions = false)]
    public string IpAddress { get; set; } = "10.185.192.24";

    [YamlMember(Alias = "port", ApplyNamingConventions = false)]
    public int Port { get; set; } = 8980;
  }
}
