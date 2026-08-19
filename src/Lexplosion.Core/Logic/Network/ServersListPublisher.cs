using System;
using System.Collections.Generic;
using NightWorld.Tools.Minecraft.NBT.StorageFiles;

namespace Lexplosion.Logic.Network
{
	/// <summary>
	/// Отображает миры друзей, принудительно добавляя их в список серверов майнкрафта (файл servers.dat).
	/// Альтернатива широковещательной рассылке, которая у части пользователей не работает
	/// из-за особенностей их сетевых настроек.
	/// </summary>
	class ServersListPublisher
	{
		/// <summary>
		/// Префикс имени сервера, по которому определяется, что запись в servers.dat добавлена лаунчером.
		/// Нужен, чтобы при очистке не задеть сервера, добавленные самим пользователем.
		/// В майнкрафте выглядит как курсив (§o) того же цвета, что и широковещательная рассылка (§3).
		/// </summary>
		private const string OurEntryNamePrefix = "§3§o";

		/// <summary>
		/// Адрес, на котором ClientBridge поднимает локальные сокеты для миров друзей.
		/// </summary>
		private const string LocalAddress = "127.0.0.1";

		private readonly string _serversDatPath;
		private readonly object _locker = new object();

		/// <param name="serversDatPath">Путь до файла servers.dat нужного клиента.</param>
		public ServersListPublisher(string serversDatPath)
		{
			_serversDatPath = serversDatPath;
		}

		/// <summary>
		/// Мир друга, который нужно отобразить в списке серверов.
		/// </summary>
		public struct WorldInfo
		{
			/// <summary>
			/// Имя, под которым мир будет отображаться в списке серверов.
			/// </summary>
			public readonly string Name;

			/// <summary>
			/// Локальный порт, который ClientBridge выделил под этот мир.
			/// </summary>
			public readonly int Port;

			public WorldInfo(string name, int port)
			{
				Name = name;
				Port = port;
			}
		}

		/// <summary>
		/// Приводит список серверов в servers.dat в соответствие с переданным списком миров:
		/// добавляет отсутствующие записи и убирает свои устаревшие.
		/// </summary>
		/// <param name="worlds">Актуальный список миров друзей.</param>
		public void Sync(IEnumerable<WorldInfo> worlds)
		{
			var actualServers = new List<ServersDatManager.ServerData>();

			if (worlds != null)
			{
				foreach (var world in worlds)
				{
					actualServers.Add(new ServersDatManager.ServerData(
						OurEntryNamePrefix + world.Name,
						LocalAddress + ":" + world.Port
					));
				}
			}

			Apply(actualServers);
		}

		/// <summary>
		/// Убирает из servers.dat все записи, добавленные лаунчером.
		/// </summary>
		public void Clear()
		{
			Apply(new List<ServersDatManager.ServerData>());
		}

		private void Apply(List<ServersDatManager.ServerData> actualServers)
		{
			lock (_locker)
			{
				try
				{
					// Файл каждый раз перечитывается, чтобы не затереть правки,
					// которые пользователь мог сделать в списке серверов внутри игры.
					var serversDat = new ServersDatManager(_serversDatPath);

					// Убираем свои записи, которых больше нет в актуальном списке. Сюда же попадают
					// записи, оставшиеся от предыдущего запуска, если лаунчер закрылся аварийно.
					int removedCount = serversDat.RemoveServers(
						(server) => IsOurEntry(server) && !Contains(actualServers, server)
					);

					int addedCount = 0;
					foreach (var server in actualServers)
					{
						if (serversDat.ContsainsServer(server.Name, server.Ip)) continue;

						serversDat.AddServer(server);
						addedCount++;
					}

					// Если список не изменился, файл не трогаем.
					if (removedCount == 0 && addedCount == 0) return;

					if (serversDat.SaveFile())
					{
						Runtime.DebugWrite($"servers.dat updated. Added: {addedCount}, removed: {removedCount}");
					}
					else
					{
						Runtime.DebugWrite("servers.dat saving failed");
					}
				}
				catch (Exception ex)
				{
					Runtime.DebugWrite($"Exception: {ex}");
				}
			}
		}

		private static bool IsOurEntry(ServersDatManager.ServerData server)
		{
			return server.Name != null
				&& server.Ip != null
				&& server.Name.StartsWith(OurEntryNamePrefix)
				&& server.Ip.StartsWith(LocalAddress + ":");
		}

		private static bool Contains(List<ServersDatManager.ServerData> servers, ServersDatManager.ServerData server)
		{
			foreach (var item in servers)
			{
				if (item.Name == server.Name && item.Ip == server.Ip) return true;
			}

			return false;
		}
	}
}
