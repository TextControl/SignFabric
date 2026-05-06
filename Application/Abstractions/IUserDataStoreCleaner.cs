namespace SignFabric.Application.Abstractions {
	public interface IUserDataStoreCleaner {
		void DeleteAllStores(string userId);
	}
}
