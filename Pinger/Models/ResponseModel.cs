using System.Net.NetworkInformation;
using System.Text.Json.Serialization;

namespace Pinger.Models
{
	public class ResponseModel
	{

		[JsonConverter(typeof(JsonStringEnumConverter))]
		public IPStatus Status { get; set; }
		[JsonIgnore]
		public string Address { get; set; }
		public long RoundTripTime { get; set; }
		public string TimeStamp { get; set; }

		public static ResponseModel GoodResponse(IPStatus status, string address, long roundtrip, string timeStamp)
			=> new ResponseModel()
			{
				Status = status,
				Address = address,
				RoundTripTime = roundtrip,
				TimeStamp = timeStamp,
			};

		public static ResponseModel BadResponse(IPStatus status, string timeStamp)
			=> new ResponseModel()
			{
				Status = status,
				Address = string.Empty,
				RoundTripTime = 0,
				TimeStamp = timeStamp,
			};
	}
}
