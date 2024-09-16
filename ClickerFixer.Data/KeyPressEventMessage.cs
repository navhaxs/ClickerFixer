using System.Runtime.Serialization;

namespace ClickerFixer.Data;

[DataContract]
public class KeyPressEventMessage
{
	public int KeyCode { get; set; }
}
