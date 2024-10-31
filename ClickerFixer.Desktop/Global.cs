// Decompiled with JetBrains decompiler
// Type: ClickerFixer.Desktop.Global
// Assembly: ClickerFixer.Desktop, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 71A8AC6C-045E-4BCB-810F-1DCB1DB4956B
// Assembly location: C:\Users\Jeremy\Desktop\clicker-fixer-app\ClickerFixer.Desktop.dll

using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#nullable enable
namespace ClickerFixer.Desktop
{
  internal static class Global
  {
    private const string CONFIG_FILE = "app.yml";
    public static Config Config;
    public static bool StartAsMinimized = false;

    public static void Init()
    {
      IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
      if (File.Exists("app.yml"))
      {
        string input = File.ReadAllText("app.yml");
        Global.Config = deserializer.Deserialize<Config>(input);
      }
      else
        Global.Config = new Config();
      File.WriteAllText("app.yml", new SerializerBuilder().Build().Serialize((object) Global.Config));
    }
  }
}
