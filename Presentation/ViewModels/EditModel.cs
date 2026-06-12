using SignFabric.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Presentation.ViewModels {
	public class EditModel {
		public string Image { get; set; }
		public Envelope Envelope { get; set; }
	}

	public class EditContractModel {
		public string Image { get; set; }
		public Contract Contract { get; set; }
	}

	public class EditTemplateModel {
		public string Image { get; set; }
		public Template Template { get; set; }
	}

	public class TemplateEditModel {
		public string Document { get; set; }
		public Template Template { get; set; }
	}

	public class ContractEditModel {
		public string Document { get; set; }
		public Contract Contract { get; set; }
	}

	public class SignModel {
		public string Document { get; set; }
		public Envelope Envelope { get; set; }
		public Signer Signer { get; set; }
	}

	public class CollaborationModel {
		public string Document { get; set; }
		public Contract Contract { get; set; }
		public string User { get; set; }
		public bool Owner{ get; set; }
	}
}
