using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClickerFixer.Satellite;

internal static class Global
{
	private const string CONFIG_FILE = "app.yml";

	public static ServerConfig ServerConfig;

	public static void Init()
	{
		IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
		if (File.Exists("app.yml"))
		{
			string input = File.ReadAllText("app.yml");
			ServerConfig = deserializer.Deserialize<ServerConfig>(input);
		}
		else
		{
			ServerConfig = new ServerConfig();
		}
	}
}
