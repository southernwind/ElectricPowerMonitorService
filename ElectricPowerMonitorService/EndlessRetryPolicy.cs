using System;

using Microsoft.AspNetCore.SignalR.Client;

namespace ElectricPowerMonitorService {
	public class EndlessRetryPolicy : IRetryPolicy {
		/// <summary>
		/// リトライ前待機時間
		/// </summary>
		public int WaitSeconds {
			get;
		}

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="waitSeconds">リトライ前待機時間</param>
		public EndlessRetryPolicy(int waitSeconds) {
			this.WaitSeconds = waitSeconds;
		}

		public TimeSpan? NextRetryDelay(RetryContext retryContext) {
			return TimeSpan.FromSeconds(this.WaitSeconds);
		}
	}
}
