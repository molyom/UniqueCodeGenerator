using System.Runtime.Serialization;

namespace Molyom
{
	[DataContract]
	public class UniqueCodeGeneratorPluginConfig
	{
		[DataMember]
		public string EntityName;

		[DataMember]
		public string EventName;
	}
}
