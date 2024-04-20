using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Pinger
{
	internal partial class Program
	{
		private static ConcurrentQueue<ResponseModel> _queue = new ConcurrentQueue<ResponseModel>();
		private static ResponseModel _currentModel;
		private static bool _running = true;
		private static CancellationTokenSource _token = new CancellationTokenSource();
		private static int MAX_DATA = 100;
		static string HOUR_DATE_TIME = "HH-mm-ss";
		static string FULL_DATE_TIME = "dd-MM-yyyy_HH-mm-ss";
		private static AutoResetEvent _resetEvent = new AutoResetEvent(false);
		static void Main(string[] args)
		{
			Directory.CreateDirectory(GetFolderPath());
			var ct = _token.Token;
			Console.CancelKeyPress += Console_CancelKeyPress;

			var pingTask = Task.Run(() =>
			{
				ct.ThrowIfCancellationRequested();

				while (_running)
				{
					Ping();
					Task.Delay(500).Wait();
				};
			}, ct);

			var processTask = Task.Run(async () =>
			{
				ct.ThrowIfCancellationRequested();

				while (_running)
				{
					await ProcessAsync();
					Task.Delay(5000).Wait();
				};
			}, ct);

			var uiThread = Task.Run(() =>
			{
				ct.ThrowIfCancellationRequested();

				while (_running)
				{
					if (_resetEvent.WaitOne())
						UpdateUI();
					Task.Delay(1).Wait();
				};
			}, ct);

			while (Console.ReadKey().Key != ConsoleKey.Escape) { } //get stuck in nothing until Esc is pressed.

			Finish();
		}

		private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			Finish();
		}

		private static void Finish()
		{
			_running = false;
			_token.Cancel();
		}

		private static async Task ProcessAsync()
		{
			if (_queue.Count < MAX_DATA)
				return;

			var list = new List<ResponseModel>();

			while (_queue.TryDequeue(out var model))
			{
				list.Add(model);
			}

			await Save(list);
		}

		private static string DateTimeNow(string format) => DateTime.Now.ToString(format);

		private static async Task Save(List<ResponseModel> items)
		{
			var path = Path.Combine(GetFolderPath(), $"data_{DateTimeNow(FULL_DATE_TIME)}");

			using (var createStream = File.Create(path))
			{
				await JsonSerializer.SerializeAsync(createStream, items, options: new JsonSerializerOptions() { WriteIndented = true });
			}
		}

		private static string GetFolderPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Network Tester");

		private static void Ping()
		{
			var ipAdress = "8.8.8.8";
			var ping = new Ping();
			bool sucess = false;
			PingReply reply = ping.Send(ipAdress, 100);

			try
			{
				if (reply.Status == IPStatus.Success)
				{
					var status = reply.Status;
					var address = reply.Address.ToString();
					var roundTripTime = reply.RoundtripTime;
					sucess = true;

					_currentModel = ResponseModel.GoodResponse(status, address, roundTripTime, DateTimeNow(HOUR_DATE_TIME));
				}
				else
				{
					sucess = false;
					_currentModel = ResponseModel.BadResponse(reply.Status, DateTimeNow(HOUR_DATE_TIME));
				}
			}
			catch
			{
				sucess = false;
				_currentModel = ResponseModel.BadResponse(reply.Status, DateTimeNow(HOUR_DATE_TIME));
			}

			//if (sucess == true)
			//{
				_queue.Enqueue(_currentModel);
			//}

			_resetEvent.Set();
		}

		private static void UpdateUI()
		{
			Console.WriteLine(" Address: {0} \n Status: {1} \n RoundtripTime: {2} \n TimeStamp: {3} \n ---------------------------",
							_currentModel.Address, _currentModel.Status, _currentModel.RoundTripTime, _currentModel.TimeStamp
							);
		}
	}
}
