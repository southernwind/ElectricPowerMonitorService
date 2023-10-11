using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

using ElectricPowerMonitorService.ElectricPower.Meter;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ElectricPowerMonitorService.ElectricPower {
	public class ElectricPowerMonitor : IDisposable {
		private readonly CompositeDisposable _disposables;
		private readonly Subject<(DateTime, int)> _electricPowerReceivedSubject;
		private readonly ILogger<ElectricPowerMonitor> _logger;
		private readonly string _electricPowerMonitorId;
		private readonly string _electricPowerMonitorPassword;
		private readonly string _hubUrl;
		private readonly string _methodName;
		private bool _disposedValue;

		public ElectricPowerMonitor(ILogger<ElectricPowerMonitor> logger, IConfiguration configuration) {
			this._logger = logger;
			this._disposables = new CompositeDisposable();
			this._electricPowerReceivedSubject = new Subject<(DateTime, int)>();
			this._disposables.Add(this._electricPowerReceivedSubject);
			var epmSection = configuration.GetSection("ElectricPowerMonitor");
			this._electricPowerMonitorId = epmSection.GetSection("ID").Get<string>();
			this._electricPowerMonitorPassword = epmSection.GetSection("Password").Get<string>();
			var signalRSection = configuration.GetSection("SignalR");
			this._hubUrl = signalRSection.GetSection("HubUrl").Value;
			this._methodName = signalRSection.GetSection("MethodName").Value;
		}

		/// <summary>
		/// 監視開始
		/// </summary>
		public async Task StartAsync() {
			var hubConnection = new HubConnectionBuilder()
				.WithUrl(this._hubUrl)
				.WithAutomaticReconnect(new EndlessRetryPolicy(3))
				.Build();
			await hubConnection.StartAsync();
			this._electricPowerReceivedSubject.AsObservable().Subscribe(x => {
				try {
					hubConnection.InvokeAsync(
						this._methodName,
						x.Item1,
						x.Item2);
				} catch (Exception e) {
					this._logger.LogWarning(e, "電力量出力時エラー");
				}
				this._logger.LogTrace(x.ToString());
			});

			this.StartCore();
		}

		/// <summary>
		/// 監視開始
		/// </summary>
		private void StartCore() {
			while (!this._disposedValue) {
				try {
					this.ConnectAndMonitor();
				} catch (Exception e) {
					this._logger.LogInformation(e, "電力量監視開始時例外");
					Thread.Sleep(1000);
					continue;
				}
				break;
			}
		}

		/// <summary>
		/// 接続・監視
		/// </summary>
		private void ConnectAndMonitor() {
			var op = new MeterReader();
			this._disposables.Add(op);
			op.Initialize(this._electricPowerMonitorId, this._electricPowerMonitorPassword);
			var channel = op.ScanChannel() ?? throw new Exception();

			this._logger.LogInformation($"チャンネル取得[channel:{channel.Channel}] [addr:{channel.Addr}]");

			var address = op.Connect(channel);
			this._logger.LogInformation("接続完了");

			var timerLoop = Observable.Interval(TimeSpan.FromSeconds(1))
				.Synchronize()
				.Select(_ => (DateTime?)DateTime.Now)
				.Do(x => {
					if (op.Measurement(address) is not { } result) {
						return;
					}

					this._electricPowerReceivedSubject.OnNext(((DateTime, int))(x!, result));
				}).Catch((MeterReader.MeterReaderException e) => {
					this._logger.LogWarning(e, "電力量監視時例外");
					return null;
				})
				.Select(x => x == null ? null : (int?)0)
				.Aggregate((x1, x2) => x1 == null ? 1 : x2 == null ? ++x1 : 0)
				.Select(x => x >= 3 ? Observable.Empty<Unit>() : Observable.Return(Unit.Default))
				.Subscribe(_ => { }, _ => {
					// 3回連続で失敗したら接続からやり直し
					op.Dispose();
					this.StartCore();
				});
			this._disposables.Add(timerLoop);
		}

		protected virtual void Dispose(bool disposing) {
			if (this._disposedValue) {
				return;
			}

			if (disposing) {
				this._disposables.Dispose();
			}

			this._disposedValue = true;
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}
