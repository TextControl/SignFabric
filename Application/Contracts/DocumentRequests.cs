namespace SignFabric.Application.Contracts {
	public class SaveDocumentRequest {
		public string Document { get; set; }
	}

	public class SaveCollaborationDocumentRequest {
		public string Document { get; set; }
	}

	public class CreateTemplateRequest {
		public string Name { get; set; }
	}

	public class RenameTemplateRequest {
		public string Name { get; set; }
	}
}
