using SignFabric.Domain;

namespace SignFabric.Application.Contracts {
	public class NewContractModel {
		public Contract Contract { get; set; }
		public string Thumbnail { get; set; }
	}

	public class NewTemplateModel {
		public Template Template { get; set; }
		public string Thumbnail { get; set; }
	}
}
