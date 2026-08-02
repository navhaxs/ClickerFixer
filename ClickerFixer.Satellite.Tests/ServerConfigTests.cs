using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClickerFixer.Satellite.Tests;

public class ServerConfigTests
{
	[Fact]
	public void LogPort_DefaultsTo8981()
	{
		var config = new ServerConfig();

		Assert.Equal((ushort)8981, config.LogPort);
	}

	[Fact]
	public void LogPort_DeserializesFromUnderscoredYamlKey()
	{
		// Global.Init() uses UnderscoredNamingConvention, so "LogPort" must
		// read from "log_port" in app.yml, matching the existing "port" key.
		IDeserializer deserializer = new DeserializerBuilder()
			.WithNamingConvention(UnderscoredNamingConvention.Instance)
			.Build();

		var config = deserializer.Deserialize<ServerConfig>("port: 8980\nlog_port: 9001\n");

		Assert.Equal((ushort)8980, config.Port);
		Assert.Equal((ushort)9001, config.LogPort);
	}
}
