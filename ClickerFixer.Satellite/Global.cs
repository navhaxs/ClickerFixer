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
		ServerConfig = null;

		if (File.Exists("app.yml"))
		{
			try
			{
				IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
				string input = File.ReadAllText("app.yml");
				ServerConfig = deserializer.Deserialize<ServerConfig>(input);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Global] app.yml is malformed, falling back to defaults: {ex}");
			}
		}

		ServerConfig ??= new ServerConfig();
	}
}
