// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Desktop.ProPresenterConfig
// Assembly: ClickerFixer.Desktop, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Desktop.dll

using YamlDotNet.Serialization;

#nullable enable
namespace ClickerFixer.Desktop
{
  internal class ProPresenterConfig
  {
    [YamlMember(Alias = "password", ApplyNamingConventions = false)]
    public string Password { get; set; } = "sweclive";

    [YamlMember(Alias = "port", ApplyNamingConventions = false)]
    public int Port { get; set; } = 20562;
  }
}
