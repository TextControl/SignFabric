namespace SignFabric.Application.Abstractions {
	public interface ICurrentUserContext {
		string UserId { get; }
		string UserName { get; }
	}
}
