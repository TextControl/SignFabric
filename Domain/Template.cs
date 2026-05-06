using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Domain {
	public class Template {
		public int Id { get; set; }
		public string TemplateID { get; set; } 
		public string UserID { get; set; }
		public string Name { get; set; }
		public DateTime Created { get; set; }
		public bool ContainsSignatureBoxes { get; set; }
	}
}
