using System;
using System.Collections.Generic;
using System.Text;

namespace ElectricPowerMonitorService.ElectricPower.Meter.Object {
	public class EchoNetLiteFrame {
		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="ehd1">ECHONET Lite電文ヘッダー1</param>
		/// <param name="ehd2">ECHONET Lite電文ヘッダー2</param>
		/// <param name="tid">トランザクションID (2byte)</param>
		/// <param name="seoj">送信元ECHONET Liteオブジェクト指定 (3byte)</param>
		/// <param name="deoj">相手先ECHONET Liteオブジェクト指定 (3byte)</param>
		/// <param name="esv">ECHONET Liteサービス</param>
		/// <param name="opc">処理プロパティ数</param>
		/// <param name="epc">ECHONET Liteプロパティ</param>
		/// <param name="pdc">EDTのバイト数</param>
		/// <param name="edt">プロパティ値データ</param>
		public EchoNetLiteFrame(byte ehd1, byte ehd2, byte[] tid, byte[] seoj, byte[] deoj, byte esv, byte opc, byte epc, byte pdc, byte[] edt) {
			this.Ehd1 = ehd1;
			this.Ehd2 = ehd2;
			this.Tid = tid;
			this.Seoj = seoj;
			this.Deoj = deoj;
			this.Esv = esv;
			this.Opc = opc;
			this.Epc = epc;
			this.Pdc = pdc;
			this.Edt = edt;
		}

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="frameBinaryData">フレームデータ</param>
		public EchoNetLiteFrame(byte[] frameBinaryData) {
			var span = frameBinaryData.AsSpan();
			this.Ehd1 = span[0];
			this.Ehd2 = span[1];
			this.Tid = span[2..4].ToArray();
			this.Seoj = span[4..7].ToArray();
			this.Deoj = span[7..10].ToArray();
			this.Esv = span[10];
			this.Opc = span[11];
			this.Epc = span[12];
			this.Pdc = span[13];
			this.Edt = span[14..(14 + this.Pdc)].ToArray();
		}
		/// <summary>
		/// ECHONET Lite電文ヘッダー1
		/// </summary>
		public byte Ehd1 {
			get;
		}

		/// <summary>
		/// ECHONET Lite電文ヘッダー2
		/// </summary>
		public byte Ehd2 {
			get;
		}

		/// <summary>
		/// トランザクションID (2byte)
		/// </summary>
		public byte[] Tid {
			get;
		}

		/// <summary>
		/// 送信元ECHONET Liteオブジェクト指定 (3byte)
		/// </summary>
		public byte[] Seoj {
			get;
		}

		/// <summary>
		/// 相手先ECHONET Liteオブジェクト指定 (3byte)
		/// </summary>
		public byte[] Deoj {
			get;
		}

		/// <summary>
		/// ECHONET Liteサービス
		/// </summary>
		public byte Esv {
			get;
		}

		/// <summary>
		/// 処理プロパティ数
		/// </summary>
		public byte Opc {
			get;
		}

		/// <summary>
		/// ECHONET Liteプロパティ
		/// </summary>
		public byte Epc {
			get;
		}

		/// <summary>
		/// EDTのバイト数
		/// </summary>
		public byte Pdc {
			get;
		}

		/// <summary>
		/// プロパティ値データ
		/// </summary>
		public byte[] Edt {
			get;
		}

		public byte[] GetFrame() {
			var result = new List<byte> {
				this.Ehd1,
				this.Ehd2
			};
			result.AddRange(this.Tid);
			result.AddRange(this.Seoj);
			result.AddRange(this.Deoj);
			result.Add(this.Esv);
			result.Add(this.Opc);
			result.Add(this.Epc);
			result.Add(this.Pdc);
			result.AddRange(this.Edt);
			return result.ToArray();
		}

		public string GetFrameString() {
			return Encoding.Default.GetString(this.GetFrame());
		}

		public int GetFrameSize() {
			return this.GetFrame().Length;
		}
	}
}
