using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;

using ElectricPowerMonitorService.ElectricPower.Meter.Object;

namespace ElectricPowerMonitorService.ElectricPower.Meter {
	public class MeterReader : IDisposable {
		private bool _disposedValue;
		/// <summary>
		/// シリアル接続リソース
		/// </summary>
		private SerialPort? _serialPort;
		/// <summary>
		/// 直近10回分のログ
		/// </summary>
		private readonly Queue<byte[]> _log = new Queue<byte[]>(10);

		/// <summary>
		/// 初期化処理
		/// </summary>
		/// <param name="id">ID</param>
		/// <param name="pw">Password</param>
		public void Initialize(string id, string pw) {
			foreach (var name in SerialPort.GetPortNames()) {
				this._serialPort = new SerialPort(name, 115200);
				try {
					this._serialPort.Open();
					this._serialPort.ReadTimeout = 3000;
					this.Write($"SKSETRBID {id}");
					this.ReadString();
					this.ReadString();
					this.Write($"SKSETPWD C {pw}");
					this.ReadString();
					this.ReadString();
				} catch (Exception e) {
					var sp = this._serialPort;
					this._serialPort = null;
					sp.Dispose();
					throw new MeterReaderException($"初期化エラー {name}", this.GetLogs(), e);
				}
				break;
			}
		}

		/// <summary>
		/// チャンネルスキャン
		/// </summary>
		/// <returns>見つけたチャンネルオブジェクト or null(みつからず)</returns>
		public ChannelObject? ScanChannel() {
			if (this._serialPort == null) {
				throw new InvalidOperationException();
			}
			try {
				this._serialPort.ReadTimeout = 30_000;
				var duration = 4;
				while (duration < 8) {
					this.Write($"SKSCAN 2 FFFFFFFF {duration}");
					var keyValues = new Dictionary<string, string>();
					while (true) {
						var line = this.ReadString();

						if (line.StartsWith("EVENT 22")) {
							break;
						}

						if (!line.StartsWith("  ")) {
							continue;
						}

						var lineValue = line.Trim().Split(":");
						keyValues.Add(lineValue[0], lineValue[1]);
					}

					if (keyValues.Any()) {
						return new ChannelObject(keyValues);
					}

					duration++;
				}

				return null;
			} catch (Exception e) {
				throw new MeterReaderException("チャンネルスキャンエラー", this.GetLogs(), e);
			}
		}

		/// <summary>
		/// 接続
		/// </summary>
		/// <param name="channel">接続先チャンネル情報</param>
		/// <returns>接続先IPv6アドレス</returns>
		public string Connect(ChannelObject channel) {
			if (this._serialPort == null) {
				throw new InvalidOperationException();
			}

			try {
				this._serialPort.ReadTimeout = 5000;
				this.Write($"SKSREG S2 {channel.Channel}");
				this.ReadString();
				this.ReadString();
				this.Write($"SKSREG S3 {channel.PanId}");
				this.ReadString();
				this.ReadString();
				this.Write($"SKLL64 {channel.Addr}");
				this.ReadString();
				var address = this.ReadString().Trim();
				this.Write($"SKJOIN {address}");
				this.ReadString();
				this.ReadString();

				while (true) {
					var line = this.ReadString();

					if (line.StartsWith("EVENT 24")) {
						throw new MeterReaderException("接続失敗", this.GetLogs());
					}

					if (line.StartsWith("EVENT 25")) {
						break;
					}
				}
				this.ReadString();
				return address;
			} catch (Exception e) when (e is not MeterReaderException) {
				throw new MeterReaderException("接続エラー", this.GetLogs(), e);
			}
		}

		/// <summary>
		/// 計測
		/// </summary>
		/// <param name="ipv6Address">計測先IPv6アドレス</param>
		/// <returns>瞬時電力計測値</returns>
		public int? Measurement(string ipv6Address) {
			if (this._serialPort == null) {
				throw new InvalidOperationException();
			}

			try {
				var elf = CreateFrame();
				var sendData = Encoding.ASCII.GetBytes($"SKSENDTO 1 {ipv6Address} 0E1A 1 {elf.GetFrameSize():X4} ")
					.Concat(elf.GetFrame()).ToArray();
				this._serialPort.Write(sendData, 0, sendData.Length);
				this.ReadString();
				this.ReadString();
				this.ReadString();
				var bytes = this.ReadBytes();

				if (!Encoding.ASCII.GetString(bytes).StartsWith("ERXUDP")) {
					throw new MeterReaderException("受信エラー", this.GetLogs());
				}

				var frame = new EchoNetLiteFrame(bytes.SkipWhile(x => x != 0x10).ToArray());
				if (!frame.Seoj.SequenceEqual(new byte[] { 0x02, 0x88, 0x01 }) || frame.Esv != 0x72 ||
					frame.Epc != 0xE7) {
					return null; // 計測か通信の失敗
				}

				return BitConverter.ToInt32(BitConverter.IsLittleEndian ? frame.Edt.Reverse().ToArray() : frame.Edt.ToArray());
			} catch (Exception e) when (e is not MeterReaderException) {
				throw new MeterReaderException("計測エラー", this.GetLogs(), e);
			}
		}

		private static EchoNetLiteFrame CreateFrame() {
			return new EchoNetLiteFrame(
				0b0001_0000,
				0b1000_0001,
				new byte[] { 123, 45 }, // 任意値
				new byte[] { 0x05, 0xFF, 0x01 }, // 管理・操作関連機器クラス,コントローラ,インスタンスコード1
				new byte[] { 0x02, 0x88, 0x01 }, // 住宅・設備関連機器クラス,低圧スマート電力量メータ,インスタンスコード1
				0x62, // Get
				1,
				0xE7, // 瞬時電力計測値
				0,
				new byte[] { }
			);
		}

		/// <summary>
		/// メッセージの送信
		/// </summary>
		/// <param name="command">メッセージ</param>
		private void Write(string command) {
			this._serialPort!.Write($"{command}\r\n");
		}

		/// <summary>
		/// 受信電文を文字列で読み込み
		/// </summary>
		/// <returns>受信電文(文字列)</returns>
		private string ReadString() {
			return Encoding.ASCII.GetString(this.ReadBytes());
		}

		/// <summary>
		/// 受信電文をバイナリで読み込み
		/// </summary>
		/// <returns>受信電文(バイナリ)</returns>
		private byte[] ReadBytes() {
			var result = new List<byte>();
			while (true) {
				var b = this._serialPort!.ReadByte();
				if (b == 10) {
					break;
				}
				result.Add((byte)b);
			}

			var resultArray = result.ToArray();

			this.AddLog(resultArray);

			return resultArray;
		}

		/// <summary>
		/// ログの追加
		/// </summary>
		/// <param name="log">ログ</param>
		private void AddLog(byte[] log) {
			if (this._log.Count >= 10) {
				this._log.Dequeue();
			}
			this._log.Enqueue(log);
		}

		/// <summary>
		/// ログの取得
		/// </summary>
		/// <returns>ログ</returns>
		private string[] GetLogs() {
			return this._log.Select(Encoding.ASCII.GetString).Concat(new[] { this._serialPort?.ReadExisting() ?? "シリアルポート[null]" }).ToArray();
		}

		protected virtual void Dispose(bool disposing) {
			if (!this._disposedValue) {
				if (disposing) {
					this._serialPort?.Dispose();
				}

				this._disposedValue = true;
			}
		}

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public class MeterReaderException : Exception {
			public string[] Logs {
				get;
			}

			public MeterReaderException(string? message, string[] logs) : base(message) {
				this.Logs = logs;
			}

			public MeterReaderException(string? message, string[] logs, Exception innerException) : base(message, innerException) {
				this.Logs = logs;
			}

			public override string ToString() {
				return $"{base.ToString()}\n\n例外直前のログ:\n{string.Join("\n", this.Logs)}";
			}
		}
	}
}
