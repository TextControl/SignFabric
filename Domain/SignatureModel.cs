using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignFabric.Domain {

	public class SignatureModel {
		public string Document { get; set; }
		public int NumPages { get; set; }
		public string SignerInitials { get; set; }
		public string SignerName { get; set; }
		public DateTime TimeStamp { get; set; }
		public string UniqueId { get; set; }
		public string IPAddress { get; set; }

	}
}
