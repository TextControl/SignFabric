using SignFabric.Application.Abstractions;
using SignFabric.Infrastructure.Configuration;
using System.IO;

namespace SignFabric.Infrastructure.Storage {
	public class UserDataStoreCleaner : IUserDataStoreCleaner {
		private readonly AppSettingsPathResolver _paths;

		public UserDataStoreCleaner(AppSettingsPathResolver paths) {
			_paths = paths;
		}

		public void DeleteAllStores(string userId) {
			var userDirectory = _paths.GetUserDatabaseDirectory(userId);
			if (Directory.Exists(userDirectory)) {
				Directory.Delete(userDirectory, recursive: true);
			}

			string[] legacyFiles = Directory.GetFiles(_paths.DatabaseDirectory, "*" + userId + "*");
			foreach (string file in legacyFiles) {
				File.Delete(file);
			}
		}
	}
}
