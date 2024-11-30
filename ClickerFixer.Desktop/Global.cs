using System.IO;
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
      IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
      if (File.Exists(CONFIG_FILE))
      {
        string input = File.ReadAllText(CONFIG_FILE);
        Global.Config = deserializer.Deserialize<Config>(input);
      }
      else
        Global.Config = new Config();
      File.WriteAllText(CONFIG_FILE, new SerializerBuilder().Build().Serialize((object) Global.Config));
    }
  }
}
