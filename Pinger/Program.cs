using Pinger.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pinger
{
	internal partial class Program
	{
		/// <summary>
		/// Constants
		/// </summary>
		static string HOUR_FORMAT = "HH:mm:ss";
		static string DAY_YEAR_FORMAT = "dd-MM-yyyy_HH-mm-ss";
		static string DAY_FORMAT = "dd-MM-yyyy";

		/// <summary>
		/// Fields
		/// </summary>
		private static ConcurrentQueue<ResponseModel> _queue = new ConcurrentQueue<ResponseModel>();
		private static ResponseModel _currentModel;
		private static bool _running = true;
		private static AutoResetEvent _resetEvent = new AutoResetEvent(false);
		private static ConfigModel _config;
		private static int _count = 0;

		static async Task Main(string[] args)
		{
			await LoadDirectories();

			try
			{
				CreatePingTask();
				CreateProcessTask();
				CreateUITask();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}

			while (Console.ReadKey().Key != ConsoleKey.Escape) //stuck in a loop unless Esc key is pressed.
			{ }

			_running = false;
		}

		private static async Task LoadDirectories()
		{
			Directory.CreateDirectory(GetDataFolderPath());

			if(File.Exists(GetConfigPath()))
			{
				using (FileStream openStream = File.OpenRead(GetConfigPath()))
				{
					_config = JsonSerializer.Deserialize<ConfigModel>(openStream);
				}
			} 
			else
			{
				_config = new ConfigModel();
				await SaveJson(_config, GetConfigPath());
			}
		}

		private static void CreatePingTask()
		{
			Task.Run(() =>
			{
				while (_running)
				{
					Ping();
					Task.Delay(_config.RequestInterval).Wait();
				};
			});
		}

		private static void CreateProcessTask()
		{
			Task.Run(async () =>
			{
				while (_running)
				{
					await ProcessAsync();
					Task.Delay(5000).Wait();
				};
			});
		}

		private static void CreateUITask()
		{
			Task.Run(() =>
			{
				while (_running)
				{
					if (_count > 1000)
					{
						Console.Clear();
						_count = 0;
					}

					if (_resetEvent.WaitOne())
					{
						UpdateUI();
						_count++;
					}

					Task.Delay(1).Wait();
				};
			});
		}

		private static async Task ProcessAsync()
		{
			if (_queue.Count < _config.MaxData)
				return;

			var list = new List<ResponseModel>();

			while (_queue.TryDequeue(out var model))
			{
				list.Add(model);
			}

			var path = Path.Combine(GetDataFolderPath(), $"data_{DateTimeNow(DAY_YEAR_FORMAT)}.csv");
			await SaveCsv(list, path);
		}

		private async static Task SaveCsv(List<ResponseModel> list, string path)
		{
			try
			{
				using (var w = new StreamWriter(path))
				{
					string header = string.Join(",",
									  nameof(ResponseModel.Address),
									  nameof(ResponseModel.Status),
									  nameof(ResponseModel.RoundtripTime),
									  nameof(ResponseModel.TimeStamp));

					await w.WriteLineAsync(header);

					foreach (var model in list)
					{
						var line = string.Format("{0},{1},{2},{3}",
							model.Address,
							model.Status,
							model.RoundtripTime,
							model.TimeStamp);

						await w.WriteLineAsync(line);
						w.Flush();
					}
				}
			}
			catch(Exception ex)
			{
				_exceptions.Add(ex.Message);
			}
		}

		private static string DateTimeNow(string format) => DateTime.Now.ToString(format);

		private static async Task SaveJson<T>(T item, string path)
		{
			using (var createStream = File.Create(path))
			{
				await JsonSerializer.SerializeAsync(createStream, item, options: new JsonSerializerOptions() {  WriteIndented = true });
			}
		}

		private static string GetRootFolderPath() => 
			Path.Combine
				(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
				"Network Tester");

		private static string GetDataFolderPath() => Path.Combine(GetRootFolderPath(), "Data", DateTimeNow(DAY_FORMAT));
		private static string GetConfigPath() => Path.Combine(GetRootFolderPath(), "config.json");

		private static void Ping()
		{
			bool sucess = false;

			try
			{
				var ping = new Ping();
				PingReply reply = ping.Send(_config.Address, _config.Timeout);

				if (reply.Status == IPStatus.Success)
				{
					var status = reply.Status;
					var address = reply.Address.ToString();
					var roundTripTime = reply.RoundtripTime;
					sucess = true;

					_currentModel = ResponseModel.GoodResponse(status, address, roundTripTime, DateTimeNow(HOUR_FORMAT));
				}
				else
				{
					_currentModel = ResponseModel.BadResponse(reply.Status, DateTimeNow(HOUR_FORMAT));
				}
			}
			catch (Exception ex)
			{
				_currentModel = ResponseModel.BadResponse(IPStatus.Unknown, DateTimeNow(HOUR_FORMAT));
			}

			if (sucess == false)
			{
				_queue.Enqueue(_currentModel);
			}

			_resetEvent.Set();
		}

		private static void UpdateUI()
		{
			Console.WriteLine(" IP: {0} \n Status: {1} \n Roundtrip Time: {2}ms \n TimeStamp: {3} \n ---------------------------",
							_currentModel.Address, _currentModel.Status, _currentModel.RoundtripTime, _currentModel.TimeStamp
							);
		}
	}
}
