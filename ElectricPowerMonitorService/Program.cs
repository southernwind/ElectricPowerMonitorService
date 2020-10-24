using System;
using System.Threading.Tasks;

using ElectricPowerMonitorService.ElectricPower;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ElectricPowerMonitorService {
	public class Program {
		public static async Task Main() {
			var builder = new ConfigurationBuilder()
				.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
				.AddJsonFile("appsettings.json", false, true);
			var configuration = builder.Build();

			var serviceProvider = new ServiceCollection()
				.AddLogging(x => {
					x.AddConfiguration(configuration.GetSection("Logging"));
					x.AddConsole();
				})
				.AddScoped<IConfiguration>(_ => configuration)
				.AddScoped<ElectricPowerMonitor>()
				.BuildServiceProvider();

			var epm = serviceProvider.GetService<ElectricPowerMonitor>();
			var isCanceled = false;
			Console.CancelKeyPress += (_, _) => {
				epm.Dispose();
				isCanceled = true;
			};
			// 監視開始
			await epm.StartAsync();

			// 永久ループ
			while (!isCanceled) {
				Console.ReadLine();
			}
		}
	}
}
