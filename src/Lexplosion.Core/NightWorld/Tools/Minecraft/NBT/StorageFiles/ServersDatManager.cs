using System;
using System.Collections.Generic;
using System.IO;

namespace NightWorld.Tools.Minecraft.NBT.StorageFiles
{
	public class ServersDatManager
	{
		public struct ServerData
		{
			public string Name;
			public string Ip;
			public string ImageBase64;
			public bool? AcceptTextures;

			public ServerData(string name, string ip)
			{
				Name = name;
				Ip = ip;
				ImageBase64 = null;
				AcceptTextures = null;
			}

			public ServerData(string name, string ip, string imageBase64, bool? acceptTextures)
			{
				Name = name;
				Ip = ip;
				ImageBase64 = imageBase64;
				AcceptTextures = acceptTextures;
			}
		}

		private NbtCompound _data = null;
		private readonly List<ServerData> _servers = new List<ServerData>();
		private string _filePath = null;

		public string FilePath { set => _filePath = value; }

		public IEnumerable<ServerData> Servers { get { return _servers; } }

		public bool ContsainsServer(string name, string ip)
		{
			foreach (var server in _servers)
			{
				if (server.Name == name && server.Ip == ip)
				{
					return true;
				}
			}

			return false;
		}

		public ServersDatManager()
		{
			_data = DefaultStruct();
		}

		public ServersDatManager(string filePath)
		{
			_filePath = filePath;
			try
			{
				if (!File.Exists(_filePath))
				{
					_data = DefaultStruct();
					return;
				}

				byte[] fileBytes = File.ReadAllBytes(_filePath);
				loadData(fileBytes);
			}
			catch
			{
				_data = DefaultStruct();
			}
		}

		public ServersDatManager(byte[] fileBytes)
		{
			loadData(fileBytes);
		}

		private void loadData(byte[] fileBytes)
		{
			NbtDocoder decoder = new NbtDocoder();
			INbtNode data0;
			try
			{
				data0 = decoder.Load(fileBytes);
			}
			catch
			{
				data0 = DefaultStruct();
			}

			if (!(data0 is NbtCompound)) data0 = DefaultStruct();

			NbtCompound data = (NbtCompound)data0;
			if (!data.Content.ContainsKey("servers") && !(data.Content["servers"] is NbtList)) data = DefaultStruct();

			NbtList list = (NbtList)data.Content["servers"];
			if (list.ListContentType != NbtTagType.Compound)
			{
				data = DefaultStruct();
				list = (NbtList)data.Content["servers"];
			}

			_data = data;

			foreach (INbtNode item0 in list)
			{
				if (!(item0 is NbtCompound)) continue;

				NbtCompound item = (NbtCompound)item0;
				if (!item.ContainsKey("ip") || !item.ContainsKey("name")) continue;
				if (!(item["ip"] is NbtString) || !(item["name"] is NbtString)) continue;

				string name = ((NbtString)item["name"]).Content;
				string ip = ((NbtString)item["ip"]).Content;
				string icon = null;
				bool? acceptTextures = null;

				if (item.ContainsKey("icon") && item["icon"] is NbtString iconBase64)
				{
					icon = iconBase64.Content;
				}

				if (item.ContainsKey("acceptTextures") && item["acceptTextures"] is NbtByte flag)
				{
					acceptTextures = flag.Content == 1;
				}

				ServerData serverData = new ServerData(name, ip, icon, acceptTextures);
				_servers.Add(serverData);
			}
		}

		private NbtCompound DefaultStruct()
		{
			return new NbtCompound()
			{
				new NbtList("servers", NbtTagType.Compound)
			};
		}

		public void AddServer(ServerData serverData)
		{
			_servers.Add(serverData);
			var serversList = (NbtList)_data["servers"];

			var server = new NbtCompound
			{
				new NbtString("ip", serverData.Ip),
				new NbtString("name", serverData.Name)
			};

			if (serverData.ImageBase64 != null)
			{
				server.Add(new NbtString("icon", serverData.ImageBase64));
			}

			if (serverData.AcceptTextures != null)
			{
				byte value = (byte)((serverData.AcceptTextures == true) ? 1 : 0);
				server.Add(new NbtByte("acceptTextures", value));
			}

			serversList.Add(server);
		}

		/// <summary>
		/// Удаляет сервер из списка.
		/// </summary>
		/// <param name="name">Имя сервера.</param>
		/// <param name="ip">Адрес сервера.</param>
		/// <returns>true, если сервер был найден и удален.</returns>
		public bool RemoveServer(string name, string ip)
		{
			return RemoveServers((server) => server.Name == name && server.Ip == ip) > 0;
		}

		/// <summary>
		/// Удаляет из списка все сервера, подходящие под условие.
		/// </summary>
		/// <param name="condition">Условие, определяющее, нужно ли удалять сервер.</param>
		/// <returns>Количество удаленных серверов.</returns>
		public int RemoveServers(Func<ServerData, bool> condition)
		{
			if (condition == null) return 0;

			// Сначала по распарсенному списку определяем, какие сервера нужно удалить.
			var forRemoving = new HashSet<string>();
			foreach (var server in _servers)
			{
				if (condition(server)) forRemoving.Add(ServerKey(server.Name, server.Ip));
			}

			if (forRemoving.Count == 0) return 0;

			_servers.RemoveAll((server) => forRemoving.Contains(ServerKey(server.Name, server.Ip)));

			// Удаляем соответствующие записи из nbt структуры.
			// Именно ее правка, а не пересборка списка с нуля, нужна для того,
			// чтобы у остальных серверов не потерялись теги, о которых этот класс не знает.
			var serversList = (NbtList)_data["servers"];
			int removedCount = 0;

			// Идем с конца, чтобы удаление не сбивало индексы еще не проверенных элементов.
			for (int i = serversList.Count - 1; i >= 0; i--)
			{
				if (!(serversList[i] is NbtCompound item)) continue;
				if (!item.ContainsKey("name") || !item.ContainsKey("ip")) continue;
				if (!(item["name"] is NbtString name) || !(item["ip"] is NbtString ip)) continue;

				if (!forRemoving.Contains(ServerKey(name.Content, ip.Content))) continue;

				serversList.RemoveAt(i);
				removedCount++;
			}

			return removedCount;
		}

		private static string ServerKey(string name, string ip)
		{
			// \n не может встретиться ни в имени, ни в адресе, поэтому подходит в качестве разделителя.
			return name + "\n" + ip;
		}

		public byte[] CompileData()
		{
			NbtEncoder encoder = new NbtEncoder();
			return encoder.Encode(_data ?? DefaultStruct());
		}

		public bool SaveFile()
		{
			try
			{
				string dir = Path.GetDirectoryName(_filePath);
				if (!Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}

				// Пишем через временный файл, чтобы прерывание записи не оставило вместо servers.dat обрубок.
				// Раньше файл писался один раз при настройке клиента, а теперь еще и во время игры,
				// поэтому попасть на прерывание записи стало реально.
				string tempFile = _filePath + ".tmp";
				File.WriteAllBytes(tempFile, CompileData());

				if (File.Exists(_filePath))
				{
					File.Replace(tempFile, _filePath, null);
				}
				else
				{
					File.Move(tempFile, _filePath);
				}

				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
