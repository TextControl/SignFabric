using System.Collections.Generic;

namespace SignFabric.Application.Contracts {
	public class FieldAssignmentState {
		public bool NeedsAssignment => Fields.Count > 0;
		public List<FieldAssignmentField> Fields { get; set; } = new();
	}

	public class FieldAssignmentField {
		public string FieldId { get; set; }
		public string Name { get; set; }
		public string Label { get; set; }
		public string FieldType { get; set; }
	}

	public class FieldAssignmentRequest {
		public List<FieldAssignmentMapping> Assignments { get; set; } = new();
	}

	public class FieldAssignmentMapping {
		public string FieldId { get; set; }
		public string SignerId { get; set; }
	}
}
