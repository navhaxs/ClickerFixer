using System;
using System.IO;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClickerFixer.Desktop
{
  internal static class Global
  {
    private const string CONFIG_FILE = "app.yml";
    public static Config Config;

    public static void Init()
    {
      Global.Config = null;

      if (File.Exists(CONFIG_FILE))
      {
        try
        {
          IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
          string input = File.ReadAllText(CONFIG_FILE);
          Global.Config = deserializer.Deserialize<Config>(input);
        }
        catch (Exception ex)
        {
          Log.Error(ex, "app.yml is malformed, falling back to defaults");
        }
      }

      Global.Config ??= new Config();
      File.WriteAllText(CONFIG_FILE, new SerializerBuilder().Build().Serialize((object) Global.Config));
    }
  }
}
