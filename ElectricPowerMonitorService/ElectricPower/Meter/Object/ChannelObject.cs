using System.Collections.Generic;

namespace ElectricPowerMonitorService.ElectricPower.Meter.Object {
	public class ChannelObject {
		public ChannelObject(IReadOnlyDictionary<string, string> scanData) {
			this.Channel = scanData["Channel"];
			this.ChannelPage = scanData["Channel Page"];
			this.PanId = scanData["Pan ID"];
			this.Addr = scanData["Addr"];
			this.Lqi = scanData["LQI"];
			this.PairId = scanData["PairID"];
		}

		public string Channel {
			get;
		}

		public string ChannelPage {
			get;
		}

		public string PanId {
			get;
		}

		public string Addr {
			get;
		}

		public string Lqi {
			get;
		}

		public string PairId {
			get;
		}
	}
}
